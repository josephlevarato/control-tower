namespace Shared;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public sealed record ParseResult(PosReport? Report, string? Error)
{
    public bool IsSuccess => Report is not null;
    public static ParseResult Ok(PosReport report) => new(report, null);
    public static ParseResult Fail(string error) => new(null, error);
}

public static class PosReportParser {
    public static ParseResult Parse(string payload)
    {
        string[] parts = payload.Split("/");
        if (parts.Length != 8)
        {
            return ParseResult.Fail("Invalid number of arguments");
        }

        if (parts[0] != "POS")
        {
            return ParseResult.Fail("Payload needs to start with \"POS\"");
        }

        string from = parts[1];
        Regex fromRegex = new Regex("^(?<flight>[A-Z0-9]{3,7})\\.FR (?<airport>[A-Z]{3})$");
        Match fromMatch = fromRegex.Match(from);
        if (!fromMatch.Success)
        {
            return ParseResult.Fail("Failed to parse flight and / or departure airport");
        }

        string to = parts[2];
        Regex toRegex = new Regex("^TO (?<airport>[A-Z]{3})$");
        Match toMatch = toRegex.Match(to);

        if (!toMatch.Success)
        {
            return ParseResult.Fail("Failed to parse destination airport");
        }

        Regex dateTimeRegex = new Regex("^(?<day>\\d{2})(?<hour>\\d{2})(?<minute>\\d{2})$");
        Match dateTimeMatch = dateTimeRegex.Match(parts[3]);
        if (!dateTimeMatch.Success)
        {
            return ParseResult.Fail("Failed to parse datetime");
        }

        int day = int.Parse(dateTimeMatch.Groups["day"].Value);
        if (1 > day || day > 31 )
        {
            return ParseResult.Fail("Day must be between 1 and 31");;
        }

        int hour = int.Parse(dateTimeMatch.Groups["hour"].Value);
        if (0 > hour || hour > 23 )
        {
            return ParseResult.Fail("Hour must be between 0 and 23");;
        }

        int minute = int.Parse(dateTimeMatch.Groups["minute"].Value);
        if (0 > minute || minute > 59 )
        {
            return ParseResult.Fail("Minutes must be between 0 and 59");;
        }

        string coordinates = parts[4];
        Regex cooRegex = new Regex("^(?<lat>[NS])(?<latDeg>\\d{2})(?<latMin>\\d{2}\\.\\d+)(?<lng>[EW])(?<lngDeg>\\d{3})(?<lngMin>\\d{2}\\.\\d+)$");
        Match cooMatch = cooRegex.Match(coordinates);

        if (!cooMatch.Success)
        {
            return ParseResult.Fail("Error parsing coordinates");
        }

        int latDeg = int.Parse(cooMatch.Groups["latDeg"].Value);
        if (latDeg > 90)
        {
            return ParseResult.Fail("Latitude cannot be over 90");
        }

        double latMin = double.Parse(cooMatch.Groups["latMin"].Value, CultureInfo.InvariantCulture);
        if (latMin >= 60)
        {
            return ParseResult.Fail("Minutes cannot be over 60");
        }

        int lngDeg = int.Parse(cooMatch.Groups["lngDeg"].Value);
        if (lngDeg > 180)
        {
            return ParseResult.Fail("Longitude cannot be over 180");
        }

        double lngMin = double.Parse(cooMatch.Groups["lngMin"].Value, CultureInfo.InvariantCulture);
        if (lngMin >= 60)
        {
            return ParseResult.Fail("Minutes cannot be over 60");
        }

        bool groundSpeedValid = int.TryParse(parts[5], out var groundSpeedKnots);

        if (!groundSpeedValid)
        {
            return ParseResult.Fail("Invalid ground speed knot");
        }

        if (groundSpeedKnots <= 0)
        {
            return ParseResult.Fail("Ground speed knot needs to be over 0");
        }

        bool fueldOnBoardValid = int.TryParse(parts[6], out var fuelOnBoardKg);

        if (!fueldOnBoardValid)
        {
            return ParseResult.Fail("Invalid fuel weight");
        }

        if (fuelOnBoardKg < 0)
        {
            return ParseResult.Fail("Fuel weight needs to be over 0");
        }

        bool flowValid = int.TryParse(parts[7], out var fuelFlowKgPerHour);

        if (!flowValid)
        {
            return ParseResult.Fail("Invalid fuel flow value");
        }

        if (fuelFlowKgPerHour < 0)
        {
            return ParseResult.Fail("Fuel flow value needs to be over 0");
        }

        double latitude = latDeg + latMin / 60;
        if (latitude > 90)
        {
            return ParseResult.Fail("Latitude cannot be over 90");
        }

        double longitude = lngDeg + lngMin / 60;
        if (longitude > 180)
        {
            return ParseResult.Fail("Longitude cannot be over 180");
        }

        if (cooMatch.Groups["lat"].Value == "S")
        {
            latitude *= -1;
        }

        if (cooMatch.Groups["lng"].Value == "W")
        {
            longitude *= -1;
        }

        PosReport report = new PosReport
        {
            FlightNumber = fromMatch.Groups["flight"].Value,
            Departure = fromMatch.Groups["airport"].Value,
            Destination = toMatch.Groups["airport"].Value,
            Latitude = Math.Round(latitude, 4),
            Longitude = Math.Round(longitude, 4),
            GroundSpeedKnots = groundSpeedKnots,
            FuelOnBoardKg = fuelOnBoardKg,
            FuelFlowKgPerHour = fuelFlowKgPerHour,
            Day = day,
            Hour = hour,
            Minute = minute,
        };

        return ParseResult.Ok(report);
    }
}