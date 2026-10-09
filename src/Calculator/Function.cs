using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Shared;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
namespace Calculator;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly IAmazonDynamoDB _dynamo;
    private readonly TimeProvider _clock;
    private readonly string _bucketName;
    private readonly string _parsedTableName;
    private readonly string _resultsTableName;

    public Function()
        : this(
            new AmazonS3Client(),
            new AmazonDynamoDBClient(),
            TimeProvider.System,
            RequiredEnv("BUCKET_NAME"),
            RequiredEnv("PARSED_TABLE_NAME"),
            RequiredEnv("RESULTS_TABLE_NAME")
        )
    { }

    public Function(
        IAmazonS3 s3,
        IAmazonDynamoDB dynamo,
        TimeProvider clock,
        string bucketName,
        string parsedTableName,
        string resultsTableName
    )
    {
        _s3 = s3;
        _dynamo = dynamo;
        _clock = clock;
        _bucketName = bucketName;
        _parsedTableName = parsedTableName;
        _resultsTableName = resultsTableName;
    }

    public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var message in sqsEvent.Records)
        {
            await ProcessAsync(message.Body, context);
        }
    }

    private async Task ProcessAsync(string body, ILambdaContext context)
    {
        CalculationRequest request = JsonSerializer.Deserialize<CalculationRequest>(body, PosJson.Options)
            ?? throw new InvalidOperationException("Invalid body");

        var getItemRequest = new GetItemRequest
        {
            TableName = _parsedTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["flightId"] = new AttributeValue { S = request.FlightId },
                ["timestamp"] = new AttributeValue { S = request.Timestamp},
            },
        };

        var parsedRecord = await _dynamo.GetItemAsync(getItemRequest);

        if (parsedRecord.Item is null || parsedRecord.Item.Count == 0)
        {
            throw new InvalidOperationException($"No parsed record for {request.FlightId} at {request.Timestamp}");
        }

        string attachmentKey = parsedRecord.Item["attachment"].S;

        using GetObjectResponse response = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = _bucketName, Key = attachmentKey });
        PosAttachment posAttachment = await JsonSerializer.DeserializeAsync<PosAttachment>(response.ResponseStream, PosJson.Options)
            ?? throw new InvalidOperationException($"No data found for {request.FlightId} at {request.Timestamp}");

        Result<Airport> airport = FlightCalculator.FindAirport(posAttachment.Destination);

        if (!airport.IsSuccess)
        {
            throw new InvalidOperationException(airport.Error);
        }

        FlightProjection flightProjection = FlightCalculator.Calculate(posAttachment, airport.Value!);

        string now = _clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        CalculationResult calculationResult = flightProjection.ToResult(posAttachment, now);

        string resultPath = $"results/{posAttachment.FlightId}-{now}.json";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = resultPath,
            ContentBody = JsonSerializer.Serialize(calculationResult, PosJson.Options),
            ContentType = "application/json",
        });

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _resultsTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["flightId"] = new AttributeValue { S = calculationResult.FlightId },
                ["timestamp"] = new AttributeValue { S = calculationResult.Timestamp },
                ["attachment"] = new AttributeValue { S = resultPath },
            },
        });

        context.Logger.LogInformation($"Successfully stored {calculationResult.FlightId} in {resultPath}. lowFuelWarning={calculationResult.LowFuelWarning}");
    }

    private static string RequiredEnv(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is not set");
}