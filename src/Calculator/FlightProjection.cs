using Shared;

namespace Calculator;

public record FlightProjection
{
    public required double DistanceNm { get; init; }
    public required int RemainingFlightTimeMinutes { get; init; }
    public required int EstimatedFuelAtArrivalKg { get; init; }
    public required bool LowFuelWarning { get; init; }
    public CalculationResult ToResult(PosAttachment attachment, string timestamp)
    {
        return new CalculationResult
        {
            FlightId = attachment.FlightId,
            Timestamp = timestamp,
            Input = new CalculationResultInput
            {
                CurrentLatitude = attachment.Latitude,
                CurrentLongitude = attachment.Longitude,
                Destination = attachment.Destination,
                GroundSpeedKnots = attachment.GroundSpeedKnots,
                FuelOnBoardKg = attachment.FuelOnBoardKg,
                FuelFlowKgPerHour = attachment.FuelFlowKgPerHour,
            },
            RemainingFlightTimeMinutes = RemainingFlightTimeMinutes,
            EstimatedFuelAtArrivalKg = EstimatedFuelAtArrivalKg,
            LowFuelWarning = LowFuelWarning,
        };
    }
}