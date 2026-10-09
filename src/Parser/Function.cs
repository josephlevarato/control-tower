using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Shared;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
namespace Parser;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly IAmazonDynamoDB _dynamo;
    private IAmazonSQS _sqs;
    private readonly string _tableName;
    private readonly string _queueUrl;

    public Function()
        : this(
            new AmazonS3Client(),
            new AmazonDynamoDBClient(),
            new AmazonSQSClient(),
            RequiredEnv("TABLE_NAME"),
            RequiredEnv("QUEUE_URL")
        )
        {}

    public Function(IAmazonS3 s3, IAmazonDynamoDB dynamoDB, IAmazonSQS sqs, string tableName, string queueUrl)
    {
        _s3 = s3;
        _dynamo = dynamoDB;
        _sqs = sqs;
        _tableName = tableName;
        _queueUrl = queueUrl;
    }

    public async Task Handler(S3Event s3Event, ILambdaContext context)
    {
        foreach (var record in s3Event.Records)
        {
            await ProcessAsync(record.S3.Bucket.Name, record.S3.Object.Key, context);
        }
    }

    private async Task ProcessAsync(string bucket, string encodedKey, ILambdaContext context)
    {
        string key = WebUtility.UrlDecode(encodedKey);

        Result<PosObjectKeyParts> parsedKey = PosObjectKey.Parse(key);

        if (!parsedKey.IsSuccess)
        {
            throw new InvalidOperationException(parsedKey.Error);
        }


        string raw;

        using (GetObjectResponse response = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }))
        using (var reader = new StreamReader(response.ResponseStream))
        {
            raw = await reader.ReadToEndAsync();
        }

        Result<PosReport> parsedRaw = PosReportParser.Parse(raw);

        if (!parsedRaw.IsSuccess)
        {
            throw new InvalidOperationException(parsedRaw.Error);
        }

        Result<ResolvedIdentity> resolvedIdentity = FlightIdentity.Resolve(parsedRaw.Value!, parsedKey.Value!.ReceivedAt);

        if (!resolvedIdentity.IsSuccess)
        {
            throw new InvalidOperationException(resolvedIdentity.Error);
        }

        PosAttachment attachment = PosAttachment.From(parsedRaw.Value!, resolvedIdentity.Value!);

        string attachmentKey = $"attachment/{attachment.FlightId}-{attachment.Timestamp}.json";

        await _s3.PutObjectAsync(new PutObjectRequest
        { 
            BucketName = bucket,
            Key = attachmentKey,
            ContentBody = JsonSerializer.Serialize(attachment, PosJson.Options),
            ContentType = "application/json"
        });

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["flightId"] = new AttributeValue { S = attachment.FlightId },
                ["timestamp"] = new AttributeValue { S = attachment.Timestamp },
                ["attachment"] = new AttributeValue { S = attachmentKey },
            },
        });

        CalculationRequest request = new () { FlightId = attachment.FlightId, Timestamp = attachment.Timestamp };

        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(request, PosJson.Options),
        });

        context.Logger.LogInformation($"Parsed {key} into {attachmentKey}");
    }

    private static string RequiredEnv(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is not set");
}