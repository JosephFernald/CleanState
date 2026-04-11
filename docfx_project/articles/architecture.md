# Architecture

## Layer Model

CleanState is built as a two-layer architecture:

```
+----------------------------+
| Unity Layer (optional)     |     Disposable projection.
| GraphView, FsmRunner       |     Reads from core via extension methods.
+-------------+--------------+     Never the source of truth.
              |
              v
+----------------------------+
| Core FSM (pure C#)         |     Source of truth.
| MachineDefinition, Machine |     Engine-agnostic. No Unity types.
| Builder, Scheduler, Debug  |     Targets netstandard2.0.
+----------------------------+
```

## Core Layer

The core layer is pure C# targeting `netstandard2.0`. It has no dependencies on Unity or any game engine.

### Responsibilities

- FSM runtime execution
- State and step definitions
- Event processing and delivery
- Recovery and checkpoint logic
- Debug tracing and breakpoints

### Key Components

| Component | Namespace | Purpose |
|---|---|---|
| `MachineBuilder` | `CleanState.Builder` | Fluent API entry point for defining machines |
| `StateBuilder` | `CleanState.Builder` | Defines states and their step pipelines |
| `DecisionBuilder` | `CleanState.Builder` | Conditional branching within states |
| `MachineDefinition` | `CleanState.Runtime` | Immutable compiled definition |
| `Machine` | `CleanState.Runtime` | Live FSM instance with state and context |
| `Scheduler` | `CleanState.Runtime` | Drives machine execution each frame |
| `EventQueue` | `CleanState.Runtime` | Queues and delivers events |
| `MachineContext` | `CleanState.Steps` | Key-value data store for step communication |
| `FsmDebugController` | `CleanState.Debug` | Opt-in debug commands (pause, resume, step, breakpoints) |

## Unity Layer

The Unity layer is strictly **observation-only**. It cannot mutate machine state, modify context, or force transitions.

### Responsibilities

- Input forwarding to the FSM
- Rendering and presentation
- Animation execution
- Event publishing back to FSM

### Integration Pattern

```csharp
MonoBehaviour -> Controller -> FSM
```

## Identity Model

CleanState avoids raw ints, rigid enums, and string comparisons at runtime:

- **Authoring** uses human-readable strings
- **Compilation** converts strings to typed int-backed structs (`StateId`, `EventId`, `MachineId`)
- **Runtime** uses only typed IDs for zero-allocation lookup
- **Debugging** uses a reverse `NameLookup` table for human-readable output

## Project Structure

```
src/CleanState/                     Core library (netstandard2.0)
  Identity/                         StateId, EventId, MachineId, NameLookup
  Steps/                            IStep, ActionStep, WaitForEventStep,
                                    WaitForTimeStep, WaitForPredicateStep,
                                    DecisionStep, TransitionStep, MachineContext
  Runtime/                          Machine, MachineDefinition, Scheduler,
                                    EventQueue, IFsmObservable
  Builder/                          MachineBuilder, StateBuilder, DecisionBuilder
  Debug/                            StepDebugInfo, TransitionTrace, TraceBuffer,
                                    FsmExecutionException, DebugSnapshot,
                                    FsmDebugController, FsmBreakpoint
  Recovery/                         MachineSnapshot, CheckpointId

tests/CleanState.Tests/             69 tests (net8.0, NUnit)

unity/CleanState.Unity/             Unity package (optional)
  Runtime/                          FsmRunner, FsmDebugRegistry, MachineExtensions
  Editor/                           FsmGraphWindow, FsmGraphView, FsmStateNode,
                                    FsmTimelinePanel, FsmRunnerInspector
```
