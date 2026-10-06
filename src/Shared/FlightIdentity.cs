namespace Shared;

using System.Globalization;

public static class FlightIdentity
{
    public static string BuildFlightId(PosReport report, DateTimeOffset receivedAt)
    {
        DateTime dt = new DateTime(receivedAt.Year, receivedAt.UtcDateTime.Month, report.Day);
        return report.FlightNumber
            + dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + report.Departure
            + report.Destination;
    }

    public static string BuildTimestamp(PosReport report, DateTimeOffset receivedAt)
    {
        DateTimeOffset dto = new DateTime(
            receivedAt.Year,
            receivedAt.UtcDateTime.Month,
            report.Day,
            report.Hour,
            report.Minute,
            0,
            DateTimeKind.Utc
        );
        return dto.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}