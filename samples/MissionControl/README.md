# MissionControl

A rocket launch sequence demonstrating `WaitForAll` and `WaitForAny` composite wait blocks — gating on multiple simultaneous conditions and racing between competing outcomes.

## What This Demonstrates

- `WaitForAll` — blocks until ALL sub-conditions are satisfied simultaneously:
  - Two events: `NavReady` and `PropulsionReady`
  - One predicate: telemetry link established
  - One timer: 2-second minimum hold period
- `WaitForAny` — blocks until the FIRST sub-condition fires:
  - `LaunchSuccess` event (nominal trajectory confirmed)
  - `Abort` event (range safety officer triggers abort)
  - 15-second safety timeout (no confirmation received)
- Three scenarios run back-to-back showing each `WaitForAny` outcome path:
  - Scenario 1: success — event arrives before timeout
  - Scenario 2: abort — abort signal wins the race
  - Scenario 3: timeout — neither event arrives, safety timer fires
- Events, time, and predicates freely mixed in a single composite wait
- Shared `MachineDefinition` reused across all three scenario runs

## Problem It Solves

Real systems often need to wait for multiple conditions simultaneously (all sensors ready) or race between outcomes (success vs. timeout). Without composite waits, you need workaround states or boolean flags:

```csharp
bool navReady, propReady, telemetryReady;
float holdTimer;

void Update() {
    if (navReady && propReady && telemetryReady && holdTimer <= 0)
        StartLaunch();  // scattered booleans, fragile ordering
}
```

`WaitForAll` and `WaitForAny` express these patterns directly in the state machine definition — no extra states, no boolean soup.

## How to Run

```bash
dotnet run --project samples/MissionControl/MissionControl.csproj
```

## What to Look For

- **Preflight gating** — subsystems check in one by one (`[SIM]` lines), but the machine stays blocked until ALL four conditions are met
- **Hold timer** — even after all events and the predicate are satisfied, the 2-second hold must elapse
- **Launch race** — `[LAUNCH]` announces the three competing outcomes, then the scenario determines which fires first
- **Scenario 1** — `LaunchSuccess` event at t=5.0s reaches orbit
- **Scenario 2** — `Abort` event at t=4.0s triggers scrub
- **Scenario 3** — no events arrive, safety timeout at t=18.0s triggers anomaly
- **Transition timeline** — each scenario shows exactly 2 transitions (Preflight → Launch → Outcome)
