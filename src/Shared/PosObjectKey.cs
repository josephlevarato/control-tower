namespace Shared;

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

public sealed record PosObjectKeyParts(string FlightId, DateTimeOffset ReceivedAt);

public static class PosObjectKey
{
    private const string Prefix = "pos/";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string Build(string flightId, DateTimeOffset receivedAt)
    {
        return Prefix
            + flightId
            + "-"
            + receivedAt.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    public static Result<PosObjectKeyParts> Parse(string key)
    {
        // S3 event notifications URL-encode the key (":" arrives as "%3A")
        string decoded = WebUtility.UrlDecode(key);

        Regex keyRegex = new Regex("^pos/(?<flightId>[A-Z0-9]+)-(?<receivedAt>.+)$");
        Match keyMatch = keyRegex.Match(decoded);

        if (!keyMatch.Success)
        {
            return Result<PosObjectKeyParts>.Fail("Object key does not match pos/{flightId}-{receivedAt}");
        }

        bool receivedAtValid = DateTimeOffset.TryParseExact(
            keyMatch.Groups["receivedAt"].Value,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var receivedAt
        );

        if (!receivedAtValid)
        {
            return Result<PosObjectKeyParts>.Fail("Invalid receivedAt timestamp in object key");
        }

        return Result<PosObjectKeyParts>.Ok(new PosObjectKeyParts(keyMatch.Groups["flightId"].Value, receivedAt));
    }
}
