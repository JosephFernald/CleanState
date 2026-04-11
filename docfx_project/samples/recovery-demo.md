# Recovery Demo

A batch processing workflow that crashes mid-flow, then recovers from a checkpoint. This demonstrates CleanState's biggest architectural differentiator.

## The Problem

Most FSM systems assume execution never stops. Real systems do:

- Power loss mid-flow
- App backgrounded or killed
- Network interruption during async work
- Server restart during a multi-step process

Result: broken state, corrupted flows, "restart from beginning" hacks.

## What This Sample Shows

- **Checkpoint capture** — snapshot machine state with domain data at stable points
- **JSON serialization** — snapshot serialized to 185 bytes of portable JSON
- **Complete destruction** — the machine and scheduler are fully destroyed
- **Fresh restoration** — brand new machine created and restored from snapshot
- **Correct resumption** — execution continues from exactly where it stopped
- **Zero data loss** — final totals match as if no crash occurred

## Architecture

```
PHASE 1: Normal execution
  Initialize → ProcessItem (x3) → CRASH!
  Snapshot captured at last checkpoint

PHASE 2: Recovery
  New scheduler + new machine (from same definition)
  Restore from snapshot
  ProcessItem (x4 remaining) → Finalize
  Total: $1630 (same as uninterrupted run)
```

## The Machine Definition

[!code-csharp[Machine Definition](../../samples/RecoveryDemo/Program.cs#L225-L276 "Batch processor definition")]

## Key Moments

### Checkpoint Capture

At each stable point, a snapshot is captured with the domain data that matters:

```csharp
snapshot = MachineRecovery.CaptureSnapshot(machine,
    "processedCount", "totalValue", "totalItems");
```

### The Crash

After processing 3 of 7 items, the application "crashes":

```text
[CRASH] SIMULATED POWER FAILURE!
[CRASH] Items processed before crash: 3
[CRASH] Total value before crash: $555
```

### Serialized Snapshot

The snapshot is serialized to JSON — portable, inspectable, tiny:

```json
{
  "MachineName": "BatchProcessor",
  "StateName": "ProcessItem",
  "StepIndex": 0,
  "DomainData": {
    "processedCount": 2,
    "totalValue": 470,
    "totalItems": 7
  }
}
```

### Restoration

A brand new scheduler and machine are created — as if the app just restarted:

```csharp
var newScheduler = new Scheduler();
var newMachine = newScheduler.CreateMachine(definition, newTraceBuffer);
MachineRecovery.RestoreFromSnapshot(newMachine, restoredSnapshot, time);
```

### Correct Resumption

The machine picks up exactly where it left off:

```text
[RESTORE] Restored at: 2 items processed, $470 total
[RESTORE] Resuming execution...
[PROCESS] Item 3: value=$85 (running total: $555)
...
[PROCESS] Item 7: value=$290 (running total: $1630)
[FINAL  ] All 7 items processed. Total value: $1630
```

### The Proof

```text
[PROOF] The machine was destroyed and recreated.
[PROOF] Domain data survived the interruption.
[PROOF] Execution resumed from the last checkpoint.
[PROOF] No data loss. No restart. No hacks.
```

## Running It

```bash
dotnet run --project samples/RecoveryDemo/RecoveryDemo.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/RecoveryDemo/Program.cs "Recovery Demo — full source")]
