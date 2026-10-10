namespace StatusLookup;

public record LookupResponse
{
    public required string FlightId { get; init; }
    public required string Timestamp { get; init; }
    public required int RemainingFlightTimeMinutes { get; init; }
    public required int EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
}
