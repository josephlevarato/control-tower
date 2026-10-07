using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
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

    // xUnit creates a new instance for every test, so this runs before each one.
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

    [Fact]
    public async Task MalformedBodyTest()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest { Body = "not a pos report" };

        var response = await _function.Handler(request, new TestLambdaContext());
        Assert.Equal(400, response.StatusCode);
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
    }

    [Fact]
    public async Task ImpossibleDateTest()
    {
        var march1st = new FixedClock(new DateTimeOffset(2026, 3, 1, 0, 1, 0, TimeSpan.Zero));
        var function = new Function(_s3, march1st, "test-bucket");
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = "POS/UL204.FR RGN/TO BKK/311200/N1642.3E09612.5/450/12500/2800"
        };

        var response = await function.Handler(request, new TestLambdaContext());
        Assert.Equal(400, response.StatusCode);
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default!);
    }

    [Fact]
    public async Task HappyPathTest()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest { Body = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800" };
        var response = await _function.Handler(request, new TestLambdaContext());
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"flightId\":\"UL20420260904RGNBKK\",\"status\":\"RECEIVED\"}", response.Body);
        await _s3.ReceivedWithAnyArgs().PutObjectAsync(default!);
    }

    [Fact]
    public async Task Based64Request()
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800"));
        var request = new APIGatewayHttpApiV2ProxyRequest { Body = encoded, IsBase64Encoded = true };
        var response = await _function.Handler(request, new TestLambdaContext());
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"flightId\":\"UL20420260904RGNBKK\",\"status\":\"RECEIVED\"}", response.Body);
        await _s3.ReceivedWithAnyArgs().PutObjectAsync(default!);
    }
}