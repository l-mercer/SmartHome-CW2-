# Smart Home Reliability Evidence Generator (CW2)

This repository demonstrates reliable programming techniques for a safety-critical Smart Home application using .NET 8. It serves as evidence for the "Reliable Programming" coursework.

## Project Structure

- **SmartHome.Core**: Contains the domain logic, reliability modules (validation, deduplication, correlation, audit), and interfaces.
- **SmartHome.App**: A Console Application that runs specific scenarios to generate evidence (logs/screenshots).
- **SmartHome.Tests**: xUnit test project covering key reliability requirements.

## How to Run

### Prerequisites
- .NET 8 SDK installed.

### Build and Test
1. **Build Solution:**
   ```bash
   dotnet build
   ```
2. **Run Tests:**
   ```bash
   dotnet test
   ```

### Run Execution Scenarios (Evidence)
To generate the console output for your report:
```bash
dotnet run --project SmartHome.App
```
*Note: This will also create/append to `audit.log` in the execution directory.*

## Scenarios Demonstrated

The console app runs the following scenarios automatically:

1. **Validation at Boundaries**: Rejects an event with invalid values (Value=5 for Door) and invalid signature.
2. **Idempotency (Deduplication)**: Sends the same event ID twice; shows the second one being ignored.
3. **Bad vs Refactored Logic**:
   - **Bad Logic**: Triggers a break-in alarm immediately on a single sensor event (False Alarm).
   - **Refactored Logic**: Waits for correlation (Door + Motion within 10s) before confirming. Shows "Suspected" state first.
4. **Reliability & Fallback**: Simulates an SMS provider failure during a critical Fire incident, demonstrating automatic fallback to Push notification.
5. **State Machine Integrity**: Attempts an invalid state transition (Confirmed -> Detected) and catches the domain exception.

## Evidence for Report

Taking screenshots of the console output is recommended for the following sections:

1. **Input Validation**: Show the "SCENARIO 1" section where the invalid event is rejected.
2. **Deduplication**: Show "SCENARIO 2" where the duplicate event is ignored.
3. **Bad vs Good Logic**: Show the side-by-side comparison where Bad Logic fires immediately, but Refactored Logic waits for confirmation.
4. **Resilience/Fallback**: Show "SCENARIO 5" where SMS fails (Audit: NotificationRetry/NotificationFallback) and Push succeeds.
5. **Audit Logging**: Show the `[AUDIT]` lines in the console or open the `audit.log` file.

## Reliability Techniques Implemented

- **Validation**: `SensorIngestValidator` checks schema, ranges, timestamps, and signatures.
- **Idempotency**: `DeduplicationStore` prevents processing the same event ID twice.
- **Correlation**: `AlertConfirmationEngine` reduces false alarms by requiring multi-sensor evidence.
- **State Machine**: `IncidentService` enforces valid state transitions (e.g., cannot go from Confirmed back to Detected).
- **Fault Tolerance**: `NotificationService` implements timeouts, bounded retries, and channel fallback (SMS -> Push -> Email).
- **Auditability**: `AuditLog` records all critical decisions and failures to an append-only log.

