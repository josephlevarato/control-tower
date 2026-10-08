using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
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
        string body = request.IsBase64Encoded
            ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body))
            : request.Body;

        DateTimeOffset receivedAt = _clock.GetUtcNow();

        if (string.IsNullOrEmpty(body))
        {
            return BadRequest("Invalid payload");
        }

        Result<PosReport> result = PosReportParser.Parse(body);

        if (!result.IsSuccess && result.Error != null)
        {
            context.Logger.LogWarning($"Error while parsing {body}");
            return BadRequest(result.Error);
        }

        Result<ResolvedIdentity> identity = FlightIdentity.Resolve(result.Value!, receivedAt);

        if (!identity.IsSuccess && identity.Error != null)
        {
            return BadRequest(identity.Error);
        }

        string flightId = identity.Value!.FlightId;

        string key = PosObjectKey.Build(flightId, receivedAt);

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            ContentBody = body,
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