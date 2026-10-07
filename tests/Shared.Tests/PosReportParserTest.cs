namespace Shared.Tests;

public class PosReportParserTest {
    [Fact]
    public void HappyPathTest()
    {
        string validPayload = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        var receivedAt = new DateTimeOffset(2026, 9, 4, 15, 12, 30, TimeSpan.Zero);

        Result<PosReport> result = PosReportParser.Parse(validPayload);

        Assert.True(result.IsSuccess);
        Assert.IsType<PosReport>(result.Value);

        Result<ResolvedIdentity> identity = FlightIdentity.Resolve(result.Value, receivedAt);
        Assert.True(identity.IsSuccess);
        Assert.Equal("UL20420260904RGNBKK", identity.Value!.FlightId);
        Assert.Equal("UL204", result.Value.FlightNumber);
        Assert.Equal("RGN", result.Value.Departure);
        Assert.Equal("BKK", result.Value.Destination);
        Assert.Equal("2026-09-04T12:05:00Z", identity.Value.Timestamp);
        Assert.Equal(16.705, result.Value.Latitude);
        Assert.Equal(96.2083, result.Value.Longitude);
        Assert.Equal(450, result.Value.GroundSpeedKnots);
        Assert.Equal(12500, result.Value.FuelOnBoardKg);
        Assert.Equal(2800, result.Value.FuelFlowKgPerHour);
    }

    [Fact]
    public void InvalidParametersNumberTest()
    {
        string payload = "UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid number of arguments", result.Error);
    }

    [Fact]
    public void MissingPosParameterTest()
    {
        string payload = "NOTPOS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Payload needs to start with \"POS\"", result.Error);
    }

    [Fact]
    public void MalformedDepartureParameterTest()
    {
        string payload = "POS/ULZXCVB204.FR R44GN/TO BKK/041205/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to parse flight and / or departure airport", result.Error);
    }

    [Fact]
    public void MalformedArrivalParameterTest()
    {
        string payload = "POS/UL204.FR RGN/TO BANGKOK/041205/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> result = PosReportParser.Parse(payload);
        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to parse destination airport", result.Error);
    }

    [Fact]
    public void MalformedDateTimeTest()
    {
        string tooManyDateNumbersPayload = "POS/UL204.FR RGN/TO BKK/12042026/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(tooManyDateNumbersPayload);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Failed to parse datetime", r1.Error);

        string actualStringDatePayload = "POS/UL204.FR RGN/TO BKK/ABCDEF/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(actualStringDatePayload);
        Assert.False(r2.IsSuccess);
        Assert.Equal("Failed to parse datetime", r2.Error);
    }

    [Fact]
    public void InvalidDateTimeTest()
    {
        string zeroesPayload = "POS/UL204.FR RGN/TO BKK/000000/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(zeroesPayload);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Day must be between 1 and 31", r1.Error);

        string overlowingDayPayload = "POS/UL204.FR RGN/TO BKK/320426/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(overlowingDayPayload);
        Assert.False(r2.IsSuccess);
        Assert.Equal("Day must be between 1 and 31", r2.Error);

        string wrongHoursPayload = "POS/UL204.FR RGN/TO BKK/029901/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r3 = PosReportParser.Parse(wrongHoursPayload);
        Assert.False(r3.IsSuccess);
        Assert.Equal("Hour must be between 0 and 23", r3.Error);

        string wrongMinutesPayload = "POS/UL204.FR RGN/TO BKK/020599/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r4 = PosReportParser.Parse(wrongMinutesPayload);
        Assert.False(r4.IsSuccess);
        Assert.Equal("Minutes must be between 0 and 59", r4.Error);
    }

    [Fact]
    public void MalformedCoordinatesTest()
    {
        string gibberishCoordinates = "POS/UL204.FR RGN/TO BKK/041205/COORDINATES/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(gibberishCoordinates);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Error parsing coordinates", r1.Error);
    }

    [Fact]
    public void IncorrectCoordinatesFormatTest()
    {
        string wrongLat = "POS/UL204.FR RGN/TO BKK/041205/N9242.3E09612.5/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(wrongLat);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Latitude cannot be over 90", r1.Error);

        string wrongLatMin = "POS/UL204.FR RGN/TO BKK/041205/N1672.3E09612.5/450/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(wrongLatMin);
        Assert.False(r2.IsSuccess);
        Assert.Equal("Minutes cannot be over 60", r2.Error);

        string wrongLng = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E20012.5/450/12500/2800";
        Result<PosReport> r3 = PosReportParser.Parse(wrongLng);
        Assert.False(r3.IsSuccess);
        Assert.Equal("Longitude cannot be over 180", r3.Error);

        string wrongLngMin = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09697.3/450/12500/2800";
        Result<PosReport> r4 = PosReportParser.Parse(wrongLngMin);
        Assert.False(r4.IsSuccess);
        Assert.Equal("Minutes cannot be over 60", r4.Error);
    }

    [Fact]
    public void BadCalculatedCoordinatesTest()
    {
        string badLatitude = "POS/UL204.FR RGN/TO BKK/041205/N9030.0E09612.5/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(badLatitude);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Latitude cannot be over 90", r1.Error);

        string badLongitude = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E18012.5/450/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(badLongitude);
        Assert.False(r2.IsSuccess);
        Assert.Equal("Longitude cannot be over 180", r2.Error);
    }

    [Fact]
    public void BadSpeedKnotValuesTest()
    {
        string stringSpeedKnot = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/FAST/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(stringSpeedKnot);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Invalid ground speed knot", r1.Error);

        string timeFreeze = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/0/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(timeFreeze);
        Assert.False(r2.IsSuccess);
        Assert.Equal("Ground speed knot needs to be over 0", r2.Error);

        string negativeSpeed = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/-450/12500/2800";
        Result<PosReport> r3 = PosReportParser.Parse(negativeSpeed);
        Assert.False(r3.IsSuccess);
        Assert.Equal("Ground speed knot needs to be over 0", r3.Error);
    }

    [Fact]
    public void BadFuelOnBoardValuesTest()
    {
        string stringFuelQuantity = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/AFULLTANK/2800";
        Result<PosReport> r1 = PosReportParser.Parse(stringFuelQuantity);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Invalid fuel weight", r1.Error);

        string emptyTanks = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/0/2800";
        Result<PosReport> r2 = PosReportParser.Parse(emptyTanks);
        Assert.True(r2.IsSuccess);
        Assert.Null(r2.Error);

        string negativeWeight = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/-800/2800";
        Result<PosReport> r3 = PosReportParser.Parse(negativeWeight);
        Assert.False(r3.IsSuccess);
        Assert.Equal("Fuel weight needs to be over 0", r3.Error);
    }

    [Fact]
    public void BadFuelFlowValuesTest()
    {
        string stringFuelFlow = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/SLURP";
        Result<PosReport> r1 = PosReportParser.Parse(stringFuelFlow);
        Assert.False(r1.IsSuccess);
        Assert.Equal("Invalid fuel flow value", r1.Error);

        string planeOnGround = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/0";
        Result<PosReport> r2 = PosReportParser.Parse(planeOnGround);
        Assert.True(r2.IsSuccess);
        Assert.Null(r2.Error);

        string greenEnergy = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/-2900";
        Result<PosReport> r3 = PosReportParser.Parse(greenEnergy);
        Assert.False(r3.IsSuccess);
        Assert.Equal("Fuel flow value cannot be negative", r3.Error);
    }

    [Fact]
    public void ValidBoundariesTest()
    {
        string validDate = "POS/UL204.FR RGN/TO BKK/312359/N1642.3E09612.5/450/12500/2800";
        Result<PosReport> r1 = PosReportParser.Parse(validDate);
        Assert.True(r1.IsSuccess);
        Assert.Null(r1.Error);

        string validCoordinates = "POS/UL204.FR RGN/TO BKK/312359/N9000.0E18000.0/450/12500/2800";
        Result<PosReport> r2 = PosReportParser.Parse(validCoordinates);
        Assert.True(r2.IsSuccess);
        Assert.Null(r2.Error);

        /*
        string emptyTanks = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/0/2800";
        ParseResult r2 = PosReportParser.Parse(emptyTanks);
        Assert.True(r2.IsSuccess);
        Assert.Null(r2.Error);

        string planeOnGround = "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/0";
        ParseResult r3 = PosReportParser.Parse(planeOnGround);
        Assert.True(r3.IsSuccess);
        Assert.Null(r.Error);
        */
    }
}