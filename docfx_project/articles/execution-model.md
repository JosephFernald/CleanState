# Execution Model

## Core Principle

> **Run immediately until blocked. Sleep until awakened.**

A machine executes steps sequentially in a single call until it hits a blocking condition or completes. This is fundamentally different from frame-driven FSMs that poll every frame.

## Machine Lifecycle

Each FSM:

1. Executes steps sequentially
2. Continues execution in a single call until it reaches a blocking condition or completes
3. The scheduler only ticks machines that are blocked with a satisfiable condition

## Machine Status

A machine is always in one of these states:

| Status | Description |
|---|---|
| `Idle` | Created but not yet started |
| `Running` | Actively executing steps |
| `Blocked` | Waiting for an event, time, or predicate |
| `Completed` | All steps in the final state have executed |
| `Faulted` | An exception occurred during step execution |

## Blocking Conditions

Steps can block execution until a condition is met:

### WaitForEvent

Blocks until a specific named event is delivered to the machine.

```csharp
.WaitForEvent("PlayerPicked", "WaitForPick")
```

### WaitForTime

Blocks until the current time exceeds a target duration from when the wait began.

```csharp
.WaitForTime(2.5, "WaitHalfSecond")
```

### WaitForPredicate

Blocks until a predicate function returns `true`. Evaluated each scheduler tick.

```csharp
.WaitUntil(ctx => ctx.Get<int>("score") > 100, "WaitForHighScore")
```

## Scheduler

The `Scheduler` drives machine execution each frame:

```csharp
scheduler.Update(currentTime);
```

The scheduler:

- Checks which machines are runnable
- Runs only those machines
- Skips idle machines entirely

Only blocked machines with satisfiable conditions are ticked. Idle machines cost nothing.

## Step Types

### Action Steps

Execute an action and immediately continue:

```csharp
.TransitionIn(ctx => Initialize(ctx), "Init")
.Then(ctx => Process(ctx), "Process")
```

### Transition Steps

Immediately transition to another state:

```csharp
.GoTo("NextState", "GoToNext")
```

### Decision Steps

Branch to different states based on conditions:

```csharp
.Decision("CheckResult")
    .When(ctx => ctx.Get<bool>("won"), "Victory", "PlayerWon")
    .Otherwise("GameOver", "PlayerLost")
```

## Event Queue

The `EventQueue` queues events for delivery to machines. Events can be:

- **Targeted** — delivered to a specific machine
- **Broadcast** — delivered to all machines managed by the scheduler

```csharp
var eventId = MachineBuilder.EventIdFrom(definition, "PlayerPicked");
machine.SendEvent(eventId, currentTime);
```
