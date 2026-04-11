# Design Principles

CleanState is built on ten core design principles that guide every architectural and implementation decision.

## 1. Engine-Agnostic Core

The core FSM library is pure C# targeting `netstandard2.0`. It has zero dependencies on Unity, MonoBehaviour, coroutines, or any engine-specific types. This makes it portable, testable, and reusable across platforms.

## 2. Event-Driven Execution

Machines progress through events, not polling. External systems publish events; the FSM reacts. This eliminates wasted per-frame computation and makes execution flow explicit.

## 3. Run-Until-Blocked Model

When a machine starts executing, it runs as many steps as possible in a single call until it hits a blocking condition (event, time, predicate) or completes. Idle machines cost nothing.

## 4. Recovery from Stable Checkpoints

Recovery restores machines from domain truth — the meaningful data of your system — not from raw FSM position. Checkpoints mark well-defined, resumable points in execution.

## 5. Explicit Step Identity

Every step in a machine has a name, type, index, and optional source location. There are no anonymous lambdas lost in a chain. When something fails, you know exactly which step, in which state, in which machine.

## 6. Transitions Carry Provenance

Every transition records where it came from, where it went, why it happened, and when. This makes state machines auditable and debuggable by design.

## 7. Performance-First Runtime

The builder may allocate freely — it runs once. The runtime must not allocate during normal execution. No LINQ in hot paths. No string comparisons at runtime. String names are compiled to typed int-backed IDs at build time.

## 8. Debugging Is First-Class

Debugging is not an afterthought bolted onto the system. Breakpoints, trace buffers, debug snapshots, and step-level visibility are core features, not plugins.

## 9. Definition Is the Source of Truth

The compiled `MachineDefinition` is immutable and authoritative. No external system — including the Unity editor — can modify it at runtime. The definition describes what the machine *is*; the `Machine` instance tracks where it *is*.

## 10. Observation Only — No Implicit Coupling

The Unity debugger and any external tooling observe through `IFsmObservable`, a read-only interface. Debug commands go through an explicit, opt-in `FsmDebugController`. The observation layer can never affect production behavior.
