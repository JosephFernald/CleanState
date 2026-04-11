# Debugging

CleanState treats debugging as a first-class feature, not an afterthought.

## Step Debug Info

Every step carries metadata for identification:

- Machine name
- State name
- Step index
- Step type
- Label (human-readable)
- Source file and line number (optional)

This solves the "which part of the fluent chain failed?" problem.

## Exception Wrapping

All step execution is wrapped with context:

```csharp
try { Execute(); }
catch (Exception ex)
{
    throw new FsmExecutionException(stepInfo, ex);
}
```

When a step throws, `FsmExecutionException` includes the full debug info — machine name, state, step index, label, and source location — so you know exactly where and why it failed.

## Transition Tracing

Every transition records full provenance:

```text
AwaitPick -> Reveal
Reason: EventReceived
Detail: PlayerPicked
Time: 12.34s
```

Each `TransitionTrace` contains:

- From state
- To state
- Trigger step
- Reason kind (`Direct`, `DecisionBranch`, `EventReceived`, etc.)
- Timestamp

## Trace Buffer

The `TraceBuffer` stores recent transitions in a ring buffer (configurable size, default 128 entries). This enables post-mortem analysis of execution history.

## Debug Snapshots

At runtime, a machine exposes a `DebugSnapshot` containing:

- Current state
- Current step index
- Block reason
- Last event
- Last transition
- Machine status

## FsmDebugController

The `FsmDebugController` provides opt-in debug commands:

### Pause / Resume

```csharp
debugController.Pause();
// Machine stops executing
debugController.Resume();
// Machine continues from where it stopped
```

### Step Once

```csharp
debugController.StepOnce();
// Execute exactly one step, then pause again
```

### Jump to State

```csharp
debugController.JumpToState(targetStateId);
// Force-transition to a specific state (debug only)
```

### Breakpoints

Breakpoints can be set on:

- **State entry** — pause when entering a state
- **Step execution** — pause at a specific step index
- **Transition reason** — pause when a transition fires for a given reason

```csharp
debugController.AddBreakpoint(FsmBreakpoint.OnStateEntry(stateId));
debugController.AddBreakpoint(FsmBreakpoint.OnStep(stateId, stepIndex));
```

## Debug Boundary

The debugger observes through `IFsmObservable` — a read-only interface. It cannot:

- Modify `MachineContext` data
- Force transitions without `FsmDebugController`
- Drive step execution from editor hooks
- Cache state that feeds back into the FSM

This strict boundary ensures the debugger never affects production behavior.

## Unity Visual Debugger

When using CleanState with Unity, a GraphView-based debugger is available at **Window > CleanState > FSM Debugger**:

- Live state highlighting with status indicators
- Step-level visibility within each state
- Transition reason tracking on edges
- Timeline panel with trace playback
- Click-to-set breakpoints
