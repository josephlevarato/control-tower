# Control Tower

## Goal

This project's goal is to be able to track a plane's info and internal status: 
- Flight ID
- Departure
- Arrival
- GPS coordinates
- Fuel stock
- Fuel consumption
- Speed

---

## Requirements

- Node.js <= 22.20.0
- .NET 10 SDK
- AWS cli configured with credentials

## Installation

```
cd infra/ && npm ci
```
```
cd infra && cdk deploy
```




## Decisions

### Ingest Lambda

- Uses the HTTP API (V2) over REST API (v1)
- Returns 200 on success
- In order to retrieve the right flightId, the Parser lambda will read it back from the s3 key

---

## Known issues

### No clock skew-tolerence

If the aircraft's clock runs one minute fast at midnight, a report stamped `05 00:00` can arrive at `04 23:59`.
