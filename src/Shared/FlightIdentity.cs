namespace Shared;

using System.Globalization;

public sealed record ResolvedIdentity(string FlightId, string Timestamp);

public static class FlightIdentity
{
    // The report only carries day/hour/minute. A report can't come from the future,
    // so a day later than today's means the report was sent last month.
    public static Result<ResolvedIdentity> Resolve(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime utc = receivedAt.UtcDateTime;
        DateTime month = report.Day > utc.Day ? utc.AddMonths(-1) : utc;

        if (report.Day > DateTime.DaysInMonth(month.Year, month.Month))
        {
            return Result<ResolvedIdentity>.Fail("Report day does not exist in the resolved month");
        }

        DateTime date = new DateTime(month.Year, month.Month, report.Day, report.Hour, report.Minute, 0, DateTimeKind.Utc);

        string flightId = report.FlightNumber
            + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + report.Departure
            + report.Destination;
        string timestamp = date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return Result<ResolvedIdentity>.Ok(new ResolvedIdentity(flightId, timestamp));
    }
}
