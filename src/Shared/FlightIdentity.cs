namespace Shared;

using System.Globalization;

public sealed record IdentityResult(string? Value, string? Error)
{
    public bool IsSuccess => Value is not null;
    public static IdentityResult Ok(string value) => new(value, null);
    public static IdentityResult Fail(string error) => new(null, error);
}

public static class FlightIdentity
{
    private const string ImpossibleDateError = "Report day does not exist in the resolved month";

    public static IdentityResult BuildFlightId(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime? date = ResolveReportDate(report, receivedAt);
        if (date is null)
        {
            return IdentityResult.Fail(ImpossibleDateError);
        }

        return IdentityResult.Ok(
            report.FlightNumber
            + date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + report.Departure
            + report.Destination
        );
    }

    public static IdentityResult BuildTimestamp(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime? date = ResolveReportDate(report, receivedAt);
        if (date is null)
        {
            return IdentityResult.Fail(ImpossibleDateError);
        }

        return IdentityResult.Ok(date.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }

    private static DateTime? ResolveReportDate(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime utc = receivedAt.UtcDateTime;
        DateTime month = report.Day > utc.Day ? utc.AddMonths(-1) : utc;

        if (report.Day > DateTime.DaysInMonth(month.Year, month.Month))
        {
            return null;
        }

        return new DateTime(month.Year, month.Month, report.Day, report.Hour, report.Minute, 0, DateTimeKind.Utc);
    }
}
