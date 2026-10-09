using System.Collections.Frozen;
using Shared;

namespace Calculator;

public class FlightCalculator
{
    const double EarthRadiusNauticalMiles = 3440.065;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static readonly FrozenDictionary<string, Airport> airports = new Dictionary<string, Airport>
    {
        { "RGN", new Airport{ Latitude = 16.9073, Longitude = 96.1332 } },
        { "BKK", new Airport{ Latitude = 13.6900, Longitude = 100.7501 } },
        { "SIN", new Airport{ Latitude = 1.3644, Longitude = 103.9915 } },
        { "KTI", new Airport{ Latitude = 11.3600, Longitude = 104.9213 } },
        { "KUL", new Airport{ Latitude = 2.7456, Longitude = 101.7100 } }
    }.ToFrozenDictionary();

    public static FlightProjection Calculate(PosAttachment attachment, Airport airport)
    {
        double dLat = ToRadians(airport.Latitude - attachment.Latitude);
        double dLon = ToRadians(airport.Longitude - attachment.Longitude);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(attachment.Latitude)) * Math.Cos(ToRadians(airport.Latitude))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        double distanceNm = EarthRadiusNauticalMiles * c;
        double remainingTime = distanceNm / attachment.GroundSpeedKnots;
        double fuelAtArrival = attachment.FuelOnBoardKg - (attachment.FuelFlowKgPerHour * remainingTime);

        return new FlightProjection
        {
            DistanceNm = distanceNm,
            RemainingFlightTimeMinutes = Math.Round(remainingTime * 60, MidpointRounding.AwayFromZero),
            EstimatedFuelAtArrivalKg = Math.Round(fuelAtArrival, MidpointRounding.AwayFromZero),
            LowFuelWarning = fuelAtArrival < 0,
        };
    }

    public static Result<Airport> FindAirport(string code) =>
        airports.TryGetValue(code, out var airport)
            ? Result<Airport>.Ok(airport)
            : Result<Airport>.Fail($"Unknown destination airport: {code}");
}
