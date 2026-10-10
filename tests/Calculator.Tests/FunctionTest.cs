using Amazon.Lambda.TestUtilities;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Lambda.SQSEvents;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using Shared;

namespace Calculator.Tests;

public class FunctionTest
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IAmazonDynamoDB _dynamo = Substitute.For<IAmazonDynamoDB>();
    private readonly Function _function;

    public FunctionTest()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 4, 12, 6, 15, 842, TimeSpan.Zero));
        _function = new Function(_s3, _dynamo, clock, "test-bucket", "test-parsed-table", "test-result-table");
    }

    private static SQSEvent EventWith(string body)
    {
        return new()
        {
            Records =
            [
                new SQSEvent.SQSMessage{
                    Body = body,
                }
            ]
        };
    }

    private void GivenParsedRecord(string attachmentKey)
    {
        _dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["attachment"] = new AttributeValue { S = attachmentKey },
                },
            });
    }

    private void GivenAttachment(PosAttachment attachment)
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(attachment, PosJson.Options)) });
    }

    private static readonly PosAttachment WorkedExample = new ()
        {
            FlightId = "UL20420260904RGNBKK",
            FlightNumber = "UL204",
            Departure = "RGN",
            Destination = "BKK",
            Timestamp = "2026-09-04T12:05:00Z",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = 12500,
            FuelFlowKgPerHour = 2800,
        };

    const string SqsBody = """{"flightId":"UL20420260904RGNBKK","timestamp":"2026-09-04T12:05:00Z"}""";

    [Fact]
    public async Task HappyPathFunctionTest()
    {
        // Arrange
        GivenParsedRecord("attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json");
        GivenAttachment(WorkedExample);
        var sqsEvent = EventWith(SqsBody);

        // Act
        await _function.Handler(sqsEvent, new TestLambdaContext());

        // Assert
        string expected = """{"flightId":"UL20420260904RGNBKK","timestamp":"2026-09-04T12:06:15.842Z","input":{"currentLatitude":16.705,"currentLongitude":96.2083,"destination":"BKK","groundSpeedKnots":450,"fuelOnBoardKg":12500,"fuelFlowKgPerHour":2800},"remainingFlightTimeMinutes":43,"estimatedFuelAtArrivalKg":10513,"lowFuelWarning":false}""";

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.ContentBody == expected &&
                r.Key == "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json"),
            Arg.Any<CancellationToken>());

        await _dynamo.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.TableName == "test-result-table" && 
                r.Item["flightId"].S == "UL20420260904RGNBKK" &&
                r.Item["timestamp"].S == "2026-09-04T12:06:15.842Z" &&
                r.Item["attachment"].S == "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json"
            ),
            Arg.Any<CancellationToken>());

        await _s3.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(r => r.Key == "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task MissingParsedRecordTest()
    {
        // Arrange
        _dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>()).Returns(new GetItemResponse());
        var sqsEvent = EventWith(SqsBody);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => _function.Handler(sqsEvent, new TestLambdaContext()));

        // Assert
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
        await _dynamo.DidNotReceiveWithAnyArgs().PutItemAsync(default!);
        await _s3.DidNotReceiveWithAnyArgs().GetObjectAsync(default!);
    }

    [Fact]
    public async Task UnknownAirportTest()
    {
        // Arrange
        GivenParsedRecord("attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json");
        GivenAttachment(WorkedExample with { Destination = "CDG" });
        var sqsEvent = EventWith(SqsBody);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => _function.Handler(sqsEvent, new TestLambdaContext()));

        // Assert
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
        await _dynamo.DidNotReceiveWithAnyArgs().PutItemAsync(default!);
    }

    [Fact]
    public async Task ResultsTableDown()
    {
        // Arrange
        GivenParsedRecord("attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json");
        GivenAttachment(WorkedExample);
        var sqsEvent = EventWith(SqsBody);
        _dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutItemResponse>>(_ => throw new AmazonDynamoDBException("down"));

        // Act
        await Assert.ThrowsAsync<AmazonDynamoDBException>(() => _function.Handler(sqsEvent, new TestLambdaContext()));

        // Assert
        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.Key == "results/UL20420260904RGNBKK-2026-09-04T12:06:15.842Z.json"),
            Arg.Any<CancellationToken>());
    }
}