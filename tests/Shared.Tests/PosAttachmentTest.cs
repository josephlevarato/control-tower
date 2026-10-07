using System.Text.Json;

namespace Shared.Tests;

public class PosAttachmentTest
{
    private string payload = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
    private string expectedJson = """{"flightId":"UL20420260904RGNBKK","flightNumber":"UL204","departure":"RGN","destination":"BKK","timestamp":"2026-09-04T12:05:00Z","latitude":16.705,"longitude":96.2083,"groundSpeedKnots":450,"fuelOnBoardKg":12500,"fuelFlowKgPerHour":2800}""";

    [Fact]
    public void HappyPathTest()
    {
        Result<PosReport> result = PosReportParser.Parse(payload);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var receivedAt = new DateTimeOffset(2026, 9, 4, 15, 12, 30, TimeSpan.Zero);
        Result<ResolvedIdentity> identity = FlightIdentity.Resolve(result.Value!, receivedAt);

        Assert.True(identity.IsSuccess);
        Assert.Null(identity.Error);

        PosAttachment attachment = PosAttachment.From(result.Value!, identity.Value!);

        string serialized = JsonSerializer.Serialize(attachment, PosJson.Options);

        Assert.Equal(expectedJson, serialized);

    }
}