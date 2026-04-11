# Unity Integration

CleanState's Unity layer is an optional adapter that provides MonoBehaviour-based scheduling and a visual debugger.

## Setup

1. Build `CleanState.dll` from the core project
2. Place the DLL in your Unity project's `Plugins/` folder
3. Copy `unity/CleanState.Unity/` into `Packages/` (or reference as a local package)
4. Add an `FsmRunner` component to a GameObject in your scene

## FsmRunner

`FsmRunner` is the primary integration point. It manages the `Scheduler` and drives `Update()` calls automatically each frame.

### Creating and Running Machines

```csharp
// Get the FsmRunner from a GameObject
var fsmRunner = GetComponent<FsmRunner>();

// Create and start a machine
var machine = fsmRunner.CreateAndStart(definition);

// Send events
var pickEvent = MachineBuilder.EventIdFrom(definition, "PlayerPicked");
fsmRunner.SendEvent(pickEvent);
```

## Core Rule

> **Core FSM must not reference Unity types.**

The Unity layer is strictly a **disposable projection**. It reads from the core via extension methods and `IFsmObservable`. It never serves as the source of truth.

### Integration Pattern

```
MonoBehaviour -> Controller -> FSM
```

Unity responsibilities:

- **Input forwarding** — translate Unity input into FSM events
- **Rendering/presentation** — display state based on FSM observations
- **Animation execution** — trigger animations from FSM callbacks
- **Event publishing** — forward completion events back to the FSM

## Visual Debugger

Open **Window > CleanState > FSM Debugger** to access the GraphView-based visual debugger.

### Features

- **Live state highlighting** — active state glows with status indicators
- **Step-level visibility** — each state node lists its steps with type-specific icons
- **Transition tracking** — last transition edge lights up with reason tooltip
- **Timeline panel** — scrub through recent transition history
- **Breakpoints** — click to set breakpoints on state entry, step execution, or transition reason

### Status Indicators

| Color | Status |
|---|---|
| Green | Running |
| Orange | Blocked |
| Blue | Completed |
| Red | Faulted |

## Synchronization

Use events instead of polling:

```csharp
// Preferred
.WaitForEvent("AnimationComplete", "WaitForAnim")

// Avoid
.WaitUntil(ctx => IsAnimDone(), "PollAnim")
```
