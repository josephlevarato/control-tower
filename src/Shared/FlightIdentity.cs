namespace Shared;

using System.Globalization;

public sealed record ResolvedIdentity(string FlightId, string Timestamp);

public sealed record IdentityResult(ResolvedIdentity? Identity, string? Error)
{
    public bool IsSuccess => Identity is not null;
    public static IdentityResult Ok(ResolvedIdentity identity) => new(identity, null);
    public static IdentityResult Fail(string error) => new(null, error);
}

public static class FlightIdentity
{
    public static IdentityResult Resolve(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime utc = receivedAt.UtcDateTime;
        DateTime month = report.Day > utc.Day ? utc.AddMonths(-1) : utc;

        if (report.Day > DateTime.DaysInMonth(month.Year, month.Month))
        {
            return IdentityResult.Fail("Report day does not exist in the resolved month");
        }

        DateTime date = new DateTime(month.Year, month.Month, report.Day, report.Hour, report.Minute, 0, DateTimeKind.Utc);

        string flightId = report.FlightNumber
            + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + report.Departure
            + report.Destination;
        string timestamp = date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return IdentityResult.Ok(new ResolvedIdentity(flightId, timestamp));
    }
}
