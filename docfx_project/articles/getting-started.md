# Getting Started

## Installation

### .NET Project

Add a reference to `CleanState.dll` in your project, or include the `CleanState.csproj` in your solution:

```bash
dotnet build src/CleanState/CleanState.csproj
```

### Unity

1. Build `CleanState.dll` and place it in your Unity project's `Plugins/` folder
2. Copy `unity/CleanState.Unity/` into `Packages/` (or reference as a local package)
3. Add `FsmRunner` to a GameObject

## Building Your First Machine

CleanState uses a fluent builder API to define state machines:

```csharp
using CleanState.Builder;

var definition = new MachineBuilder("TrafficLight")
    .State("Green")
        .TransitionIn(ctx => Console.WriteLine("Light is GREEN"), "ShowGreen")
        .WaitForTime(5.0, "GreenDuration")
        .GoTo("Yellow", "GoToYellow")
    .State("Yellow")
        .TransitionIn(ctx => Console.WriteLine("Light is YELLOW"), "ShowYellow")
        .WaitForTime(2.0, "YellowDuration")
        .GoTo("Red", "GoToRed")
    .State("Red")
        .TransitionIn(ctx => Console.WriteLine("Light is RED"), "ShowRed")
        .WaitForTime(5.0, "RedDuration")
        .GoTo("Green", "GoToGreen")
    .Build();
```

## Running the Machine

### Standalone (Pure C#)

```csharp
using CleanState.Runtime;

var scheduler = new Scheduler();
var machine = scheduler.CreateMachine(definition);
machine.Start(0.0);

// Game loop
double currentTime = 0.0;
while (machine.Status != MachineStatus.Completed)
{
    currentTime += 0.016; // ~60fps
    scheduler.Update(currentTime);
}
```

### Unity

```csharp
var machine = fsmRunner.CreateAndStart(definition);
```

The scheduler is driven automatically by `FsmRunner` each frame.

## Sending Events

Events allow external systems to drive state transitions:

```csharp
var definition = new MachineBuilder("PickGame")
    .State("AwaitPick")
        .WaitForEvent("PlayerPicked", "WaitForPick")
        .GoTo("Reveal", "GoToReveal")
    .State("Reveal")
        .TransitionIn(ctx => ProcessReveal(ctx), "ProcessReveal")
    .Build();

// Get the compiled event ID
var pickEvent = MachineBuilder.EventIdFrom(definition, "PlayerPicked");

// Send the event
machine.SendEvent(pickEvent, currentTime);
```

## Using Context Data

The `MachineContext` provides a key-value store for passing data between steps:

```csharp
var definition = new MachineBuilder("Counter")
    .State("Init")
        .TransitionIn(ctx => ctx.Set("count", 0), "InitCount")
        .GoTo("Increment", "GoToIncrement")
    .State("Increment")
        .Then(ctx => ctx.Set("count", ctx.Get<int>("count") + 1), "IncrementCount")
        .Decision("CheckCount")
            .When(ctx => ctx.Get<int>("count") >= 10, "Done", "ReachedLimit")
            .Otherwise("Increment", "KeepGoing")
    .State("Done")
        .Then(ctx => Console.WriteLine($"Final count: {ctx.Get<int>("count")}"), "ShowResult")
    .Build();
```

## Next Steps

- [Execution Model](execution-model.md) — Understand the run-until-blocked model
- [Debugging](debugging.md) — Learn about breakpoints, tracing, and the visual debugger
- [Recovery](recovery.md) — Implement checkpoint-based recovery
