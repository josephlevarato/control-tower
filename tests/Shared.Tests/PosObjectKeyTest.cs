namespace Shared.Tests;

public class PosObjectKeyTest {
    private static readonly DateTimeOffset ReceivedAt = new DateTimeOffset(2026, 9, 4, 15, 12, 30, 123, TimeSpan.Zero);

    [Fact]
    public void BuildTest()
    {
        Assert.Equal(
            "pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z",
            PosObjectKey.Build("UL20420260904RGNBKK", ReceivedAt)
        );
    }

    [Fact]
    public void BuildConvertsToUtcTest()
    {
        DateTimeOffset parisTime = new DateTimeOffset(2026, 9, 4, 17, 12, 30, 123, TimeSpan.FromHours(2));
        Assert.Equal(
            "pos/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z",
            PosObjectKey.Build("UL20420260904RGNBKK", parisTime)
        );
    }

    [Fact]
    public void RoundTripTest()
    {
        Result<PosObjectKeyParts> result = PosObjectKey.Parse(PosObjectKey.Build("UL20420260904RGNBKK", ReceivedAt));

        Assert.True(result.IsSuccess);
        Assert.Equal("UL20420260904RGNBKK", result.Value!.FlightId);
        Assert.Equal(ReceivedAt, result.Value.ReceivedAt);
    }

    [Fact]
    public void ParseUrlEncodedKeyTest()
    {
        Result<PosObjectKeyParts> result = PosObjectKey.Parse("pos/UL20420260904RGNBKK-2026-09-04T15%3A12%3A30.123Z");

        Assert.True(result.IsSuccess);
        Assert.Equal("UL20420260904RGNBKK", result.Value!.FlightId);
        Assert.Equal(ReceivedAt, result.Value.ReceivedAt);
    }

    [Fact]
    public void ParseFeedsFlightIdentityTest()
    {
        PosReport report = PosReportParser.Parse("POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800").Value!;
        string key = PosObjectKey.Build(FlightIdentity.Resolve(report, ReceivedAt).Value!.FlightId, ReceivedAt);

        Result<PosObjectKeyParts> parsed = PosObjectKey.Parse(key);
        Result<ResolvedIdentity> identity = FlightIdentity.Resolve(report, parsed.Value!.ReceivedAt);

        Assert.Equal(parsed.Value.FlightId, identity.Value!.FlightId);
        Assert.Equal("2026-09-04T12:05:00Z", identity.Value.Timestamp);
    }

    [Fact]
    public void InvalidKeyTest()
    {
        Result<PosObjectKeyParts> wrongPrefix = PosObjectKey.Parse("attachment/UL20420260904RGNBKK-2026-09-04T15:12:30.123Z");
        Assert.False(wrongPrefix.IsSuccess);
        Assert.Equal("Object key does not match pos/{flightId}-{receivedAt}", wrongPrefix.Error);

        Result<PosObjectKeyParts> noSeparator = PosObjectKey.Parse("pos/UL20420260904RGNBKK");
        Assert.False(noSeparator.IsSuccess);
        Assert.Equal("Object key does not match pos/{flightId}-{receivedAt}", noSeparator.Error);
    }

    [Fact]
    public void InvalidTimestampTest()
    {
        Result<PosObjectKeyParts> impossibleDate = PosObjectKey.Parse("pos/UL20420260904RGNBKK-2026-13-04T15:12:30.123Z");
        Assert.False(impossibleDate.IsSuccess);
        Assert.Equal("Invalid receivedAt timestamp in object key", impossibleDate.Error);

        Result<PosObjectKeyParts> noMilliseconds = PosObjectKey.Parse("pos/UL20420260904RGNBKK-2026-09-04T15:12:30Z");
        Assert.False(noMilliseconds.IsSuccess);
        Assert.Equal("Invalid receivedAt timestamp in object key", noMilliseconds.Error);
    }
}
