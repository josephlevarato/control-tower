namespace Shared;

public record PosReport
{
    public required string FlightNumber { get; init; }
    public required string Departure { get; init; }
    public required string Destination { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required int GroundSpeedKnots { get; init; }
    public required int FuelOnBoardKg { get; init; }
    public required int FuelFlowKgPerHour { get; init; }
    public required int Day { get; init; }
    public required int Hour { get; init; }
    public required int Minute { get; init; }
}