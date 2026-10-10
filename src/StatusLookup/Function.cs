using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Shared;

namespace StatusLookup;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _bucketName;
    private readonly string _parsedTableName;
    private readonly string _resultsTableName;

    public Function()
        : this(
            new AmazonS3Client(),
            new AmazonDynamoDBClient(),
            Environment.GetEnvironmentVariable("BUCKET_NAME")
                ?? throw new InvalidOperationException("BUCKET_NAME is not set"),
            Environment.GetEnvironmentVariable("PARSED_TABLE_NAME")
                ?? throw new InvalidOperationException("PARSED_TABLE_NAME is not set"),
            Environment.GetEnvironmentVariable("RESULTS_TABLE_NAME")
                ?? throw new InvalidOperationException("RESULTS_TABLE_NAME is not set")
        )
    { }

    public Function(
        IAmazonS3 s3,
        IAmazonDynamoDB dynamo,
        string bucketName,
        string parsedTableName,
        string resultsTableName
    )
    {
        _s3 = s3;
        _dynamo = dynamo;
        _bucketName = bucketName;
        _parsedTableName = parsedTableName;
        _resultsTableName = resultsTableName;
    }

     public async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context
    )
    {
        if (request.PathParameters is null
            || !request.PathParameters.TryGetValue("flightId", out string? flightId)
            || string.IsNullOrWhiteSpace(flightId))
        {
            return BadRequest("Missing :flightId parameter");
        }

        var result = await LatestItemAsync(_resultsTableName, flightId);
        if (result is null)
        {
            result = await LatestItemAsync(_parsedTableName, flightId);

            if (result is null) {
                return NotFound($"No data found for {flightId}");
            }

            // Flight details found but not yet calculated, return early.
            return Json(200, new { flightId, status = "PARSED" });
        }

        string attachmentKey = result["attachment"].S;

        using GetObjectResponse response = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = _bucketName, Key = attachmentKey });
        CalculationResult? calculationResult = await JsonSerializer.DeserializeAsync<CalculationResult>(response.ResponseStream, PosJson.Options);

        if (calculationResult is null)
        {
            return Json(500, $"Could not find matching data for {flightId}");
        }

        LookupResponse lookupResponse = new()
        {
            FlightId = calculationResult.FlightId,
            Timestamp = calculationResult.Timestamp,
            RemainingFlightTimeMinutes = calculationResult.RemainingFlightTimeMinutes,
            EstimatedFuelAtArrivalKg = calculationResult.EstimatedFuelAtArrivalKg,
            LowFuelWarning = calculationResult.LowFuelWarning,
        };

        return Json(200, lookupResponse);
    }

    private async Task<Dictionary<string, AttributeValue>?> LatestItemAsync(string tableName, string flightId)
    {
        QueryResponse response = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = tableName,
            KeyConditionExpression = "flightId = :flightId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":flightId"] = new AttributeValue { S = flightId },
            },
            ScanIndexForward = false,
            Limit = 1,
        });

        return response.Items is { Count: > 0 } ? response.Items[0] : null;
    }

    private static APIGatewayHttpApiV2ProxyResponse BadRequest(string detail) =>
          Json(400, new { error = "Invalid request", detail });

    private static APIGatewayHttpApiV2ProxyResponse NotFound(string detail) =>
          Json(404, new { error = detail });

    private static APIGatewayHttpApiV2ProxyResponse Json(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Body = JsonSerializer.Serialize(body, PosJson.Options),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
    };
}