using System.Reflection;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Runtime.Credentials.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using Shared;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Ingest;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly TimeProvider _clock;
    private string _bucketName;

    // Function called by AWS
    public Function()
        : this(
            new AmazonS3Client(),
            TimeProvider.System,
            Environment.GetEnvironmentVariable("BUCKET_NAME")
                ?? throw new InvalidOperationException("BUCKET_NAME is not set"))
    {}

    // Function called for tests
    public Function(IAmazonS3 s3, TimeProvider clock, string bucketName)
    {
        _s3 = s3;
        _clock = clock;
        _bucketName = bucketName;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context
    )
    {
        DateTimeOffset receivedAt = _clock.GetUtcNow();

        // TODO 1: request.Body null or empty → return BadRequest(...)
        if (string.IsNullOrEmpty(request.Body))
        {
            return BadRequest("Invalid payload");
        }

        // TODO 2: PosReportParser.Parse(body) → on failure: log a warning, return BadRequest(result.Error!)
        Result<PosReport> result = PosReportParser.Parse(request.Body);

        if (!result.IsSuccess && result.Error != null)
        {
            context.Logger.LogWarning($"Error while parsing {request.Body}");
            return BadRequest(result.Error);
        }

        // TODO 3: FlightIdentity.Resolve(report, receivedAt) → same handling
        Result<ResolvedIdentity> identity = FlightIdentity.Resolve(result.Value!, receivedAt);

        if (!identity.IsSuccess && identity.Error != null)
        {
            return BadRequest(identity.Error);
        }

        string flightId = identity.Value!.FlightId;

        // TODO 4: string key = PosObjectKey.Build(flightId, receivedAt);
        string key = PosObjectKey.Build(flightId, receivedAt);

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            ContentBody = request.Body,
            ContentType = "text/plain",
        });

        context.Logger.LogInformation($"Stored POS report for {flightId} at {key}");
        return Json(200, new { flightId, status = "RECEIVED"});
    }

    private static APIGatewayHttpApiV2ProxyResponse BadRequest(string detail) =>
          Json(400, new { error = "Invalid POS report", detail });

    private static APIGatewayHttpApiV2ProxyResponse Json(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
    };
}