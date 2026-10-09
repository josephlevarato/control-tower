namespace Calculator;

public record FlightProjection
{
    public required double DistanceNm { get; init; }
    public required int RemainingFlightTimeMinutes { get; init; }
    public required int EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
}