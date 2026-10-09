namespace Shared;

public record CalculationRequest
{
    public required string FlightId { get; init; }
    public required string Timestamp { get; init; }
}