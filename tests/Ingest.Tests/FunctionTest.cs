using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;

namespace Ingest.Tests;

public class FunctionTest
{
    // A clock that always returns the same time. No extra package needed.
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly Function _function;

    // xUnit creates a new instance for every test, so this runs before each one (your "setup").
    public FunctionTest()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 4, 15, 12, 30, 123, TimeSpan.Zero));
        _function = new Function(_s3, clock, "test-bucket");
    }

    [Fact]
    public async Task EmptyBodyReturns400()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest { Body = "" };

        var response = await _function.Handler(request, new TestLambdaContext());

        Assert.Equal(400, response.StatusCode);
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
    }
}