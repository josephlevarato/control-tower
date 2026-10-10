using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.DynamoDBv2;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using Shared;

namespace StatusLookup.Tests;

public class FunctionTest
{
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IAmazonDynamoDB _dynamo = Substitute.For<IAmazonDynamoDB>();
    private readonly Function _function;

    public FunctionTest()
    {
         _function = new Function(_s3, _dynamo, "test-bucket", "test-parsed-table", "test-result-table");
    }

    private void GivenLatest(string tableName, string attachmentKey) =>
        _dynamo.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [ new Dictionary<string, AttributeValue> { ["attachment"] = new AttributeValue { S = attachmentKey } } ],
            });

    private void GivenNothingIn(string tableName) =>
        _dynamo.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse());

    private void GivenStoreResponse(CalculationResult result)
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(result, PosJson.Options)) });
    }

    private APIGatewayHttpApiV2ProxyRequest RequestFor(string flightId)
    {
        return new ()
        {
            PathParameters = new Dictionary<string, string>
            {
                ["flightId"] = flightId,
            }
        };
    }

    private static readonly string Expected = """{"flightId":"UL20420260904RGNBKK","timestamp":"2026-09-04T12:06:15.842Z","remainingFlightTimeMinutes":43,"estimatedFuelAtArrivalKg":10513,"lowFuelWarning":false}""";


    private static readonly CalculationResult WorkedExample = new ()
        {
            FlightId = "UL20420260904RGNBKK",
            Timestamp = "2026-09-04T12:06:15.842Z",
            Input = new CalculationResultInput
            {
                CurrentLatitude = 16.705,
                CurrentLongitude = 96.2083,
                Destination = "BKK",
                GroundSpeedKnots = 450,
                FuelOnBoardKg = 12500,
                FuelFlowKgPerHour = 2800,
            },
            RemainingFlightTimeMinutes = 43,
            EstimatedFuelAtArrivalKg = 10513,
            LowFuelWarning = false,
        };

    [Fact]
    public async Task HappyPathCalculatedTest()
    {
        // Arrange
        GivenLatest("test-result-table", "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json");
        GivenStoreResponse(WorkedExample);
        var apiEvent = RequestFor("UL20420260904RGNBKK");

        // Act
        var response = await _function.Handler(apiEvent, new TestLambdaContext());

        // Assert
        await _dynamo.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == "test-result-table" &&
                r.ExpressionAttributeValues[":flightId"].S == "UL20420260904RGNBKK" &&
                r.ScanIndexForward == false &&
                r.Limit == 1
            ),
            Arg.Any<CancellationToken>());

        await _s3.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(r =>
                r.Key == "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json"
            ),
            Arg.Any<CancellationToken>()
        );

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(Expected, response.Body);
    }

    [Fact]
    public async Task HappyPathLowFuelTest()
    {
        // Arrange
        GivenLatest("test-result-table", "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json");
        GivenStoreResponse(WorkedExample with { LowFuelWarning = true });
        var apiEvent = RequestFor("UL20420260904RGNBKK");

        // Act
        var response = await _function.Handler(apiEvent, new TestLambdaContext());

        // Assert
        string lowFuelExpected = Expected.Replace("\"lowFuelWarning\":false", "\"lowFuelWarning\":true");
        Assert.Equal(lowFuelExpected, response.Body);
    }

    [Fact]
    public async Task HappyPathPendingCalculationTest()
    {
        // Arrange
        GivenNothingIn("test-result-table");
        GivenLatest("test-parsed-table", "attachment/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json");
        var apiEvent = RequestFor("UL20420260904RGNBKK");

        // Act
        var response = await _function.Handler(apiEvent, new TestLambdaContext());

        // Assert
        await _dynamo.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == "test-result-table"
            ),
            Arg.Any<CancellationToken>());

        await _dynamo.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == "test-parsed-table"
            ),
            Arg.Any<CancellationToken>());

        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""{"flightId":"UL20420260904RGNBKK","status":"PARSED"}""", response.Body);
    }

    [Fact]
    public async Task UnknownFlightTest()
    {
        // Arrange
        GivenNothingIn("test-result-table");
        GivenNothingIn("test-parsed-table");
        var apiEvent = RequestFor("UL20420260904RGNBKK");

        // Act
        var response = await _function.Handler(apiEvent, new TestLambdaContext());

        // Assert
        await _dynamo.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == "test-result-table"
            ),
            Arg.Any<CancellationToken>());

        await _dynamo.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == "test-parsed-table"
            ),
            Arg.Any<CancellationToken>());

        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task MissingParameterTest()
    {
        // Arrange
        var apiEvent = RequestFor("");

        // Act
        var response = await _function.Handler(apiEvent, new TestLambdaContext());

        await _dynamo.DidNotReceiveWithAnyArgs().QueryAsync(default!);
        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!);

        Assert.Equal(400, response.StatusCode);
    }


}