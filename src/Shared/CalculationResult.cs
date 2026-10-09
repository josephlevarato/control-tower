namespace Shared;

public record CalculationResultInput
{
    public required double CurrentLatitude { get; init; }
    public required double CurrentLongitude { get; init; }
    public required string Destination { get; init; }
    public required int GroundSpeedKnots { get; init; }
    public required int FuelOnBoardKg { get; init; }
    public required int FuelFlowKgPerHour { get; init; }
}

public record CalculationResult
{
    public required string FlightId { get; init; }
    public required string Timestamp { get; init; }
    public required CalculationResultInput Input { get; init; }
    public required double RemainingFlightTimeMinutes { get; init; }
    public required double EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
}
