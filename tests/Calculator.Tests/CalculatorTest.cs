using Shared;

namespace Calculator.Tests;

public class CalculatorTest
{
    [Fact]
    public void FlightCalculatorTest()
    {
        PosAttachment attachment = new PosAttachment
        {
            FlightId = "",
            FlightNumber = "",
            Departure = "RGN",
            Destination = "BKK",
            Timestamp = "",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = 12500,
            FuelFlowKgPerHour = 2800,
        };

        Airport bangkok = new () { Latitude = 13.6900, Longitude = 100.7501 };

        FlightProjection result = FlightCalculator.Calculate(attachment, bangkok);

        Assert.Equal(319.37, result.DistanceNm, 2);
        Assert.Equal(43, result.RemainingFlightTimeMinutes);
        Assert.Equal(10513, result.EstimatedFuelAtArrivalKg);
        Assert.False(result.LowFuelWarning);
    }

    [Theory]
    [InlineData(1987, true)]   // raw −0.19 kg, rounds to 0, still a warning
    [InlineData(1988, false)]  // raw +0.81 kg
    public void LowFuelWarningTest(int fuelOnBoardKg, bool expectedWarning)
    {
        Airport bangkok = new () { Latitude = 13.6900, Longitude = 100.7501 };

        PosAttachment attachment = new PosAttachment
        {
            FlightId = "",
            FlightNumber = "",
            Departure = "KUL",
            Destination = "BKK",
            Timestamp = "",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = fuelOnBoardKg,
            FuelFlowKgPerHour = 2800,
        };

        FlightProjection result = FlightCalculator.Calculate(attachment, bangkok);
        Assert.Equal(expectedWarning, result.LowFuelWarning);
    }

    [Fact]
    public void FindAirportSuccessTest()
    {
        Result<Airport> bkk = FlightCalculator.FindAirport("BKK");
        Assert.True(bkk.IsSuccess);
        Assert.Null(bkk.Error);
    }

    [Fact]
    public void FindAirportFailureTest()
    {
        Result<Airport> jfk = FlightCalculator.FindAirport("JFK");
        Assert.False(jfk.IsSuccess);
        Assert.Equal("Unknown destination airport: JFK", jfk.Error);
    }

    [Fact]
    public void FlightProjectionToResultTest()
    {
        PosAttachment attachment = new PosAttachment
        {
            FlightId = "UL20420260904RGNBKK",
            FlightNumber = "UL204",
            Departure = "RGN",
            Destination = "BKK",
            Timestamp = "2026-09-04T12:05:00Z",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = 12500,
            FuelFlowKgPerHour = 2800,
        };

        Airport bangkok = new () { Latitude = 13.6900, Longitude = 100.7501 };
        FlightProjection fpResult = FlightCalculator.Calculate(attachment, bangkok);
        CalculationResult result = fpResult.ToResult(attachment, "2026-10-09T12:05:00Z");

        Assert.Equal("UL20420260904RGNBKK", result.FlightId);
        Assert.Equal("2026-10-09T12:05:00Z", result.Timestamp);
        Assert.Equal(16.705, result.Input.CurrentLatitude);
        Assert.Equal(96.2083, result.Input.CurrentLongitude);
        Assert.Equal("BKK", result.Input.Destination);
        Assert.Equal(450, result.Input.GroundSpeedKnots);
        Assert.Equal(12500, result.Input.FuelOnBoardKg);
        Assert.Equal(2800, result.Input.FuelFlowKgPerHour);
        Assert.Equal(43, result.RemainingFlightTimeMinutes);
        Assert.Equal(10513, result.EstimatedFuelAtArrivalKg);
        Assert.False(result.LowFuelWarning);
    }
}
