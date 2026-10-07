namespace Shared;

public record PosAttachment
{
    public required string FlightId { get; init; }
    public required string FlightNumber { get; init; }
    public required string Departure { get; init; }
    public required string Destination { get; init; }
    public required string Timestamp { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required int GroundSpeedKnots { get; init; }
    public required int FuelOnBoardKg { get; init; }
    public required int FuelFlowKgPerHour { get; init; }
    public static PosAttachment From(PosReport report, ResolvedIdentity identity)
    {
        return new PosAttachment
        {
            FlightId = identity.FlightId,
            FlightNumber = report.FlightNumber,
            Departure = report.Departure,
            Destination = report.Destination,
            Timestamp = identity.Timestamp,
            Latitude = report.Latitude,
            Longitude = report.Longitude,
            GroundSpeedKnots = report.GroundSpeedKnots,
            FuelOnBoardKg = report.FuelOnBoardKg,
            FuelFlowKgPerHour = report.FuelFlowKgPerHour
        };
    }
}