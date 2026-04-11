# RecoveryDemo

Simulates a crash mid-flow, serializes machine state to JSON, then restores and resumes from the last checkpoint. This is CleanState's biggest architectural differentiator.

## What This Demonstrates

- Checkpoint capture with domain data at stable points
- JSON serialization of machine state (185 bytes)
- Complete machine and scheduler destruction
- Fresh machine created from the same definition
- Snapshot restoration with correct resumption
- Zero data loss — final totals match an uninterrupted run

## Problem It Solves

Most FSM systems assume execution never stops. Real systems do:

- Power loss mid-flow
- App backgrounded or killed
- Network interruption during async work
- Server restart during a multi-step process

The result is broken state, corrupted flows, and "restart from beginning" hacks. CleanState recovers from domain truth — the meaningful data of your system — not from raw FSM position.

## How to Run

```bash
dotnet run --project samples/RecoveryDemo/RecoveryDemo.csproj
```

## What to Look For

- **Phase 1** — watch 3 items process normally, with checkpoint captures at each stable point
- **The crash** — `SIMULATED POWER FAILURE` banner, showing exactly where execution stopped
- **JSON snapshot** — the serialized state is printed, showing how small and portable it is
- **Phase 2** — a brand new scheduler and machine are created from scratch
- **Correct resumption** — item 3 processes again (from checkpoint), then items 4-7 complete
- **Final total** — `$1630`, identical to what an uninterrupted run would produce
- **The proof** — four lines confirming destruction, survival, resumption, and no data loss
