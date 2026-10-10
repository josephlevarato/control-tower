using System.Text;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.TestUtilities;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;

namespace Parser.Test;

public class FunctionTest
{

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IAmazonDynamoDB _dynamo = Substitute.For<IAmazonDynamoDB>();
    private readonly IAmazonSQS _sqs = Substitute.For<IAmazonSQS>();
    private readonly Function _function;

    public FunctionTest()
    {
         _function = new Function(_s3, _dynamo, _sqs, "test-table", "https://test-queue");
    }

    private static S3Event EventFor(string bucket, string key) => new()
    {
        Records =
        [
            new S3Event.S3EventNotificationRecord
            {
                S3 = new S3Event.S3Entity
                {
                    Bucket = new S3Event.S3BucketEntity { Name = bucket },
                    Object = new S3Event.S3ObjectEntity { Key = key },
                },
            },
        ],
    };

    private void GivenStoredReport(string body) =>
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(body)) });

    [Fact]
    public async Task HappyPathTest()
    {
        // Arrange
        GivenStoredReport("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800");
        S3Event fakeEvent = EventFor("test-bucket", "pos/UL20420260904RGNBKK-2026-09-04T15%3A12%3A30.123Z");

        // Act
        await _function.Handler(fakeEvent, new TestLambdaContext());

        // Assert
        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json"),
            Arg.Any<CancellationToken>());

        await _dynamo.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["flightId"].S == "UL20420260904RGNBKK" &&
                r.Item["timestamp"].S == "2026-09-04T12:05:00Z" &&
                r.Item["attachment"].S == "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json"
            ),
            Arg.Any<CancellationToken>());

        await _sqs.Received(1).SendMessageAsync(
            Arg.Is<SendMessageRequest>(r =>
                r.QueueUrl == "https://test-queue" &&
                r.MessageBody == """{"flightId":"UL20420260904RGNBKK","timestamp":"2026-09-04T12:05:00Z"}"""
            ),
            Arg.Any<CancellationToken>());

        await _s3.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(r => r.Key == "pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidStoredReportTest()
    {
        // Arrange
        GivenStoredReport("garbage");
        S3Event fakeEvent = EventFor("test-bucket", "pos/UL20420260904RGNBKK-2026-09-04T15%3A12%3A30.123Z");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => _function.Handler(fakeEvent, new TestLambdaContext()));

        // Assert
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
        await _dynamo.DidNotReceiveWithAnyArgs().PutItemAsync(default!);
        await _sqs.DidNotReceiveWithAnyArgs().SendMessageAsync(default!);
    }

    [Fact]
    public async Task DynamoDBFailingTest()
    {
        // Arrange
        GivenStoredReport("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800");
        S3Event fakeEvent = EventFor("test-bucket", "pos/UL20420260904RGNBKK-2026-09-04T15%3A12%3A30.123Z");
        _dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PutItemResponse>>(_ => throw new AmazonDynamoDBException("down"));

        // Act
        await Assert.ThrowsAsync<AmazonDynamoDBException>(() => _function.Handler(fakeEvent, new TestLambdaContext()));

        // Assert
        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "attachment/UL20420260904RGNBKK-2026-09-04T12:05:00Z.json"),
            Arg.Any<CancellationToken>());

        await _dynamo.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
        await _sqs.DidNotReceiveWithAnyArgs().SendMessageAsync(default!);
    }

}