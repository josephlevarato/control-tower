using System.Text.Json;

namespace Shared.Tests;

public class CalculationResultTest
{
    [Fact]
    public void SerializesToSpecFormat()
    {
        CalculationResult result = new()
        {
            FlightId = "UL20420260904RGNBKK",
            Timestamp = "2026-09-04T12:06:15.842Z",
            Input = new CalculationResultInput
            {
                CurrentLatitude = 16.705,
                CurrentLongitude = 96.2083,
                Destination = "BKK",
                GroundSpeedKnots = 450,
                FuelOnBoardKg = 12500,
                FuelFlowKgPerHour = 2800,
            },
            RemainingFlightTimeMinutes = 43,
            EstimatedFuelAtArrivalKg = 10513,
            LowFuelWarning = false
        };

        string json = JsonSerializer.Serialize(result, PosJson.Options);

        const string expected = """{"flightId":"UL20420260904RGNBKK","timestamp":"2026-09-04T12:06:15.842Z","input":{"currentLatitude":16.705,"currentLongitude":96.2083,"destination":"BKK","groundSpeedKnots":450,"fuelOnBoardKg":12500,"fuelFlowKgPerHour":2800},"remainingFlightTimeMinutes":43,"estimatedFuelAtArrivalKg":10513,"lowFuelWarning":false}""";

        Assert.Equal(expected, json);
    }
}
