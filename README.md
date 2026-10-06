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

## Known issues

### No clock skew-tolerence

If the aircraft's clock runs one minute fast at midnight, a report stamped `05 00:00` can arrive at `04 23:59`.
