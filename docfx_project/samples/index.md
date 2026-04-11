# Samples

These samples demonstrate CleanState solving real problems — not toy examples with trivial state transitions.

Each sample is a standalone console application that you can build and run immediately.

## Running the Samples

```bash
# From the repository root:
dotnet run --project samples/PickGame/PickGame.csproj
dotnet run --project samples/UIFlow/UIFlow.csproj
dotnet run --project samples/RecoveryDemo/RecoveryDemo.csproj
dotnet run --project samples/TaskOrchestration/TaskOrchestration.csproj
dotnet run --project samples/ParallelSidecar/ParallelSidecar.csproj
```

## Sample Overview

| Sample | What It Demonstrates | Key Features |
|---|---|---|
| [Pick Game](pick-game.md) | Complex gameplay orchestration | Branching, loops, decisions, breakpoints, timeline |
| [UI Flow](ui-flow.md) | Multi-step UI orchestration | Event-driven flow, conditional branching, no booleans |
| [Recovery Demo](recovery-demo.md) | Crash recovery from checkpoints | Snapshot serialization, machine restoration, zero data loss |
| [Task Orchestration](task-orchestration.md) | Backend workflow with retries | Validation, service calls, retry with backoff, timeout |
| [Parallel Sidecar](parallel-sidecar.md) | Multiple concurrent machines | Scheduler power, sidecar machines, cross-machine events |

## What to Look For

Every sample is designed to create a moment of:

> "This is way cleaner than what I usually do."

Pay attention to:

- **Step visibility** — every step has a name and type, never anonymous
- **Transition reasons** — every state change logs exactly why it happened
- **Timeline replay** — full transition history printed at the end
- **Debug snapshots** — machine state is fully inspectable at any point
