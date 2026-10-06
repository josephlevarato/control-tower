namespace Shared.Tests;

public class FlightIdentityTest {
    private static PosReport ParseWithDayTime(string ddHHmm)
    {
        string payload = $"POS/UL204.FR RGN/TO BKK/{ddHHmm}/N1642.3E09612.5/450/12500/2800";
        return PosReportParser.Parse(payload).Report!;
    }

    [Theory]
    [InlineData("2026-09-04T15:12:00Z", "041205", "2026-09-04T12:05:00Z", "UL20420260904RGNBKK")] // spec example
    [InlineData("2026-10-01T00:01:00Z", "302358", "2026-09-30T23:58:00Z", "UL20420260930RGNBKK")] // month boundary
    [InlineData("2027-01-01T00:01:00Z", "312358", "2026-12-31T23:58:00Z", "UL20420261231RGNBKK")] // year boundary
    [InlineData("2026-02-01T00:01:00Z", "312358", "2026-01-31T23:58:00Z", "UL20420260131RGNBKK")] // January date received in February
    public void ResolvesReportDateTest(string receivedAt, string ddHHmm, string expectedTimestamp, string expectedFlightId)
    {
        PosReport report = ParseWithDayTime(ddHHmm);
        DateTimeOffset received = DateTimeOffset.Parse(receivedAt);

        IdentityResult result = FlightIdentity.Resolve(report, received);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTimestamp, result.Identity!.Timestamp);
        Assert.Equal(expectedFlightId, result.Identity.FlightId);
    }

    [Fact]
    public void ImpossibleDateTest()
    {
        PosReport report = ParseWithDayTime("301200");
        DateTimeOffset received = new DateTimeOffset(2026, 3, 1, 0, 1, 0, TimeSpan.Zero);

        IdentityResult result = FlightIdentity.Resolve(report, received);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Identity);
        Assert.Equal("Report day does not exist in the resolved month", result.Error);
    }
}
