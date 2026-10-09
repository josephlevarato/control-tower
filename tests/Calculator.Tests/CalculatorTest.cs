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

        Airport bangkok = new Airport{ Latitude = 13.6900, Longitude = 100.7501 };

        FlightProjection result = FlightCalculator.Calculate(attachment, bangkok);

        Assert.Equal(319.37, result.DistanceNm, 2);
        Assert.Equal(43, result.RemainingFlightTimeMinutes);
        Assert.Equal(10513, result.EstimatedFuelAtArrivalKg);
        Assert.False(result.LowFuelWarning);
    }

    [Fact]
    public void LowFuelWarningTest()
    {
        PosAttachment attachment1 = new PosAttachment
        {
            FlightId = "",
            FlightNumber = "",
            Departure = "SIN",
            Destination = "BKK",
            Timestamp = "",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = 1987,
            FuelFlowKgPerHour = 2800,
        };

        Airport bangkok = new Airport{ Latitude = 13.6900, Longitude = 100.7501 };

        FlightProjection r1 = FlightCalculator.Calculate(attachment1, bangkok);

        Assert.True(r1!.LowFuelWarning);

        PosAttachment attachment2 = new PosAttachment
        {
            FlightId = "",
            FlightNumber = "",
            Departure = "SIN",
            Destination = "BKK",
            Timestamp = "",
            Latitude = 16.705,
            Longitude = 96.2083,
            GroundSpeedKnots = 450,
            FuelOnBoardKg = 1988,
            FuelFlowKgPerHour = 2800,
        };

        FlightProjection r2 = FlightCalculator.Calculate(attachment2, bangkok);

        Assert.False(r2.LowFuelWarning);
    }

    [Fact]
    public void FindAirportTest()
    {
        Result<Airport> bkk = FlightCalculator.FindAirport("BKK");
        Assert.True(bkk.IsSuccess);
        Assert.Null(bkk.Error);

        Result<Airport> jfk = FlightCalculator.FindAirport("JFK");
        Assert.False(jfk.IsSuccess);
        Assert.Equal("Unknown destination airport: JFK", jfk.Error);
    }
}
