namespace Calculator;

public record FlightProjection
{
    public required double DistanceNm { get; init; }
    public required double RemainingFlightTimeMinutes { get; init; }
    public required double EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
}