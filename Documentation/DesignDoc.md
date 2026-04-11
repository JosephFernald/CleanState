# Finite State Machine (FSM) Framework — Design Document (v1)

## 1. Purpose

This FSM framework provides a **portable, low-overhead, debuggable orchestration system** for gameplay and application flows.

Primary use case:

* Complex feature flows (e.g., pick games, bonus rounds, reveal loops)
* Deterministic orchestration across multiple systems (UI, animation, math, services)
* Recovery from interruption (e.g., power loss, restart)
* Execution both inside and outside Unity

---

## 2. Goals

### Functional Goals

* Fluent, readable authoring model (LINQ-like syntax)
* Deterministic execution model
* Support for:

  * Sequential flows
  * Branching logic
  * Event-driven progression
  * Timed waits
  * Nested/sub state machines
* Recovery from persisted state (power interruption safe)

### Architectural Goals

* Core logic in **pure C# (engine-agnostic)**
* Unity used as **adapter/view layer only**
* FSM independent of:

  * `MonoBehaviour`
  * coroutines
  * Unity lifecycle

### Performance Goals

* **No per-frame allocations** in runtime execution
* **No LINQ in hot paths**
* Minimal per-frame work (sleep when idle)
* Builder may allocate; runtime should not

### Debugging Goals

* Full traceability of:

  * current state
  * current step
  * last transition
  * reason for transition
* Step-level identification (not anonymous chains)
* Source file + line capture where possible

---

## 3. Non-Goals

* Not a Unity coroutine framework
* Not a visual scripting system (future extension possible)
* Not a general-purpose async/task engine
* Not intended to replace animation/tween systems
* Not dependent on reflection-heavy or runtime-generated code

---

## 4. Architecture Overview

### Layering

```
+--------------------------+
| Unity Layer              |
| (Views, MonoBehaviours)  |
+------------+-------------+
             |
             v
+--------------------------+
| Core FSM + Domain Logic  |
| (Pure C#)                |
+--------------------------+
```

### Responsibilities

#### Core Layer

* FSM runtime
* State definitions
* Game/feature logic
* Event processing
* Recovery logic

#### Unity Layer

* Input forwarding
* Rendering/presentation
* Animation execution
* Event publishing back to FSM

---

## 5. Execution Model

### Core Principle

> **Run immediately until blocked. Sleep until awakened.**

### Machine Lifecycle

Each FSM:

* Executes steps sequentially
* Continues execution in a single call until:

  * it reaches a blocking condition
  * or completes

### Blocking Conditions

* Wait for event
* Wait for time
* Wait for predicate
* Wait for child machine completion

### Scheduler Model

Unity calls a scheduler each frame:

```csharp
scheduler.Update(Time.time);
```

Scheduler:

* checks which machines are runnable
* runs only those machines
* skips idle machines

---

## 6. Authoring Model

### Fluent Syntax (Example)

```csharp
builder.State("Reveal")
    .TransitionIn(PickGameActions.BeginReveal, "BeginReveal")
    .WaitForEvent(PickGameEvents.RevealFinished, "WaitForRevealFinished")
    .Then(PickGameActions.PresentOutcome, "PresentOutcome")
    .Decision("RevealDecision")
        .When(GameConditions.HasMorePicks, "AwaitPick", "HasMorePicks")
        .Otherwise("Done", "NoMorePicks");
```

### Key Rules

* Fluent API is **authoring only**
* Builder compiles into flat runtime representation
* No runtime chaining or LINQ execution

---

## 7. Runtime Model

### Core Concepts

* **State** → logical grouping of steps
* **Step** → smallest unit of execution
* **Machine** → current state + step index
* **Scheduler** → decides when to run machines
* **Event Queue** → wakes machines

### Step Types

1. Immediate

   * Execute and continue
2. Blocking

   * Wait for event/time/etc.
3. Frame-driven (rare)

   * Requires per-frame updates

---

## 8. Identity Model

### Requirements

* Avoid raw ints leaking globally
* Avoid enums (rigidity)
* Avoid string comparisons in runtime

### Solution

* Author with strings or keys
* Compile to **typed IDs (int-backed structs)**

Example:

```csharp
struct StateId { int Value; }
struct EventId { int Value; }
```

* IDs are scoped to machine instance
* Reverse lookup table used for debugging

---

## 9. Recovery Model

### Core Principle

> Recover from **domain truth**, not just FSM position.

### Snapshot Contains

* Logical phase (checkpoint)
* Domain data (picks, results, etc.)
* Optional step info (if needed)

### Recovery Flow

1. Load snapshot
2. Restore domain context
3. Resolve logical state
4. Re-enter machine at stable checkpoint
5. Rebuild presentation
6. Resume execution

### Checkpoints

Preferred recovery points:

* Await input
* Reveal complete
* Summary displayed
* Feature complete

Avoid:

* mid-animation
* transient intermediate steps

---

## 10. Debugging & Traceability

### Step Debug Info

Each step contains:

* Machine name
* State name
* Step index
* Step type
* Label
* Source file + line (optional)

---

### Exception Wrapping

All step execution is wrapped:

```csharp
try { Execute(); }
catch (Exception ex)
{
    throw new FsmExecutionException(stepInfo, ex);
}
```

---

### Transition Trace

Each transition records:

* From state
* To state
* Trigger step
* Reason kind
* Event/condition/branch
* Timestamp

---

### Transition Reason

```csharp
enum TransitionReasonKind
{
    Direct,
    DecisionBranch,
    EventReceived,
    TimeoutElapsed,
    PredicateSatisfied,
    ChildMachineCompleted,
    RecoveryRestore,
    ForcedJump,
    ExternalCommand
}
```

---

### Debug Snapshot

At runtime, machine exposes:

* Current state
* Current step
* Block reason
* Last event
* Last transition

---

### Optional Trace Buffer

* Ring buffer (e.g., last 128 entries)
* Enabled in debug builds
* Stores execution history

---

## 11. Performance Constraints

### Must Have

* No allocations during normal tick
* No LINQ in runtime
* No string comparisons in hot path
* Minimal branching overhead

### Allowed

* Builder allocations
* Debug-only tracing allocations

---

## 12. Unity Integration

### Unity Role

* Calls scheduler
* Forwards input events
* Executes presentation
* Publishes completion events

### Core Rule

> Core FSM must not reference Unity types.

### Integration Pattern

```csharp
MonoBehaviour -> Controller -> FSM
```

---

## 13. Synchronization Model

### Use Events, Not Polling

Preferred:

* `WaitForEvent`
* `WaitForAll`
* `WaitForAny`

Avoid:

* repeated boolean polling

---

## 14. Extensibility

Planned future extensions:

* Sub-state machines
* Parallel machine execution
* Visual tooling
* Debug UI inspector
* Data-oriented runtime optimization

---

## 15. Open Questions

* Predicate vs event trade-offs
* Snapshot versioning strategy
* Debug vs release metadata levels
* Parallel machine ownership model
* Step execution dispatch optimization

---

## 16. Key Design Principles

1. Engine-agnostic core
2. Event-driven execution
3. Run-until-blocked model
4. Recovery from stable checkpoints
5. Explicit step identity
6. Transitions carry provenance
7. Performance-first runtime
8. Debugging is first-class

---

## 17. Summary

This FSM is designed as a:

* **Deterministic orchestration engine**
* **Low-overhead runtime system**
* **Debuggable fluent DSL**
* **Portable gameplay core**

It intentionally avoids:

* Unity coupling
* hidden execution state
* implicit control flow

And prioritizes:

* clarity
* traceability
* recoverability
* performance

---
