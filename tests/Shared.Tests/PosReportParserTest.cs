using System.Globalization;

namespace Shared.Tests;

public class PosReportParserTest {
    [Fact]
    public void HappyPathTest()
    {
        string validPayload = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        var receivedAt = new DateTimeOffset(2026, 9, 4, 15, 12, 30, TimeSpan.Zero);

        ParseResult result = PosReportParser.Parse(validPayload);

        Assert.True(result.IsSuccess);
        Assert.IsType<PosReport>(result.Report);

        Assert.Equal("UL20420260904RGNBKK", FlightIdentity.BuildFlightId(result.Report, receivedAt));
        Assert.Equal("UL204", result.Report.FlightNumber);
        Assert.Equal("RGN", result.Report.Departure);
        Assert.Equal("BKK", result.Report.Destination);
        Assert.Equal("2026-09-04T12:05:00Z", FlightIdentity.BuildTimestamp(result.Report, receivedAt));
        Assert.Equal(16.705, result.Report.Latitude);
        Assert.Equal(96.2083, result.Report.Longitude);
        Assert.Equal(450, result.Report.GroundSpeedKnots);
        Assert.Equal(12500, result.Report.FuelOnBoardKg);
        Assert.Equal(2800, result.Report.FuelFlowKgPerHour);
    }

    [Fact]
    public void InvalidParametersNumberTest()
    {
        string payload = "UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        ParseResult result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid number of arguments", result.Error);
    }

    [Fact]
    public void MissingPosParameterTest()
    {
        string payload = "NOTPOS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        ParseResult result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Payload needs to start with \"POS\"", result.Error);
    }

    [Fact]
    public void MalformedDepartureParameterTest()
    {
        string payload = "POS/ULZXCVB204.FR R44GN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        ParseResult result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to parse flight and / or departure airport", result.Error);
    }

    [Fact]
    public void MalformedArrivalParameterTest()
    {
        string payload = "POS/UL204.FR RGN/TO BANGKOK/041205/N1642.3E09612.5/450/12500/2800";
        ParseResult result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to parse destination airport", result.Error);
    }

    [Fact]
    public void BadCoordinatesTest()
    {
        string payload = "POS/UL204.FR RGN/TO BKK/041205/N9030.0E09612.5/450/12500/2800";
        ParseResult result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Latitude cannot be over 90", result.Error);
    }
}