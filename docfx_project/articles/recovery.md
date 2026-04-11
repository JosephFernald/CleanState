# Recovery

CleanState provides a checkpoint-based recovery system for persisting and restoring machine state.

## Core Principle

> Recover from **domain truth**, not just FSM position.

Recovery restores the meaningful state of your system — the data that matters — rather than trying to replay execution from the beginning.

## Checkpoints

Checkpoints mark stable recovery points in your state machine. Place them at points where the machine is in a well-defined, resumable state:

```csharp
var definition = new MachineBuilder("PickGame")
    .State("AwaitPick")
        .Checkpoint()  // Safe to recover here
        .WaitForEvent("PlayerPicked", "WaitForPick")
        .GoTo("Reveal", "GoToReveal")
    .State("Reveal")
        .TransitionIn(ctx => BeginReveal(ctx), "BeginReveal")
        .WaitForEvent("RevealFinished", "WaitForReveal")
        .GoTo("AwaitPick", "BackToAwait")
    .Build();
```

### Good Checkpoint Locations

- Awaiting user input
- After a reveal completes
- Summary screens
- Feature completion points

### Avoid Checkpointing At

- Mid-animation
- Transient intermediate steps
- Between tightly coupled operations

## Capturing Snapshots

```csharp
// Capture the current machine state with specific context keys
var snapshot = MachineRecovery.CaptureSnapshot(machine, "score", "picksRemaining");
```

The snapshot contains:

- The logical phase (checkpoint)
- Domain data from `MachineContext` (specified keys)
- Optional step information

## Restoring from Snapshots

```csharp
// Create a fresh machine from the same definition
var newMachine = scheduler.CreateMachine(definition);

// Restore from the saved snapshot
MachineRecovery.RestoreFromSnapshot(newMachine, snapshot, currentTime);
```

### Recovery Flow

1. Load the persisted snapshot
2. Restore domain context (picks, results, scores, etc.)
3. Resolve the logical state from the checkpoint
4. Re-enter the machine at the stable checkpoint
5. Rebuild presentation layer
6. Resume execution

## Persistence

CleanState captures and restores snapshots but does not prescribe a serialization format. You can serialize `MachineSnapshot` with any serializer (JSON, binary, etc.) appropriate for your platform.
