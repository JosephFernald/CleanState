# Questions & Answers

Common questions about CleanState's architecture and design decisions.

---

## How is data shared between states?

Each machine has a `MachineContext` — a key-value blackboard (`Dictionary<string, object>`) that steps read and write during execution. This is how data flows between states without coupling them directly. Steps receive the context as a parameter:

```csharp
.State("Reveal")
    .TransitionIn(ctx => {
        var score = ctx.Get<int>("score");    // read
        ctx.Set("score", score + 100);        // write
    }, "UpdateScore")
```

The context is scoped to the machine instance. States don't reference each other's data directly — they communicate through the shared context, which means you can reorder, add, or remove states without breaking data flow as long as the keys are consistent.

For composed regions (multiple machines modeling one entity), each region has its own context, and the coordinator syncs cross-region state into each machine's context under `__region.{name}` keys so regions can read — but not write — each other's state.

---

## At what level are states decoupled?

States are fully decoupled at the definition level. A state is a named container of steps — it doesn't know what state came before it or what comes after. Transitions are defined as steps *within* a state (`GoTo`, `Decision`), not as edges on a graph owned by some external controller.

```csharp
.State("AwaitPick")
    .WaitForEvent("PlayerPicked", "WaitForPick")
    .GoTo("Reveal", "GoToReveal")    // transition is a step, not an external edge

.State("Reveal")
    .Decision("RevealDecision")
        .When(ctx => ctx.Get<bool>("gameOver"), "GameOver", "HitGameOver")
        .Otherwise("AwaitPick", "ContinuePicking")
```

At runtime, the compiled `MachineDefinition` is immutable — it contains the full topology (which states exist, what steps they have, where transitions go). The `Machine` instance only tracks *where* execution currently is (current state, current step index, block reason). This separation means:

- The definition describes *what the machine is*
- The machine instance tracks *where it is right now*
- No state object holds a reference to another state object

---

## How is the state machine tied to states?

The builder compiles string names into typed int-backed IDs (`StateId`, `EventId`) at build time. The `MachineDefinition` holds an array of `StateDefinition` objects, each containing its compiled `IStep[]` pipeline. At runtime:

```
MachineDefinition (immutable, shareable)
  └── StateDefinition[] (each has Id, Name, Steps[], IsCheckpoint)
        └── IStep[] (ActionStep, WaitForEventStep, DecisionStep, etc.)

Machine (mutable instance)
  └── current StateId + step index + MachineContext
```

The machine resolves states by ID through the definition's lookup table — O(1) dictionary access, no string comparisons at runtime. Multiple machine instances can share one definition safely because the definition is immutable and all mutable state lives in the `Machine` and its `MachineContext`.

---

## Why run-until-blocked instead of frame-driven?

Frame-driven FSMs call `Update()` every frame, even when nothing is happening. This wastes CPU and hides execution order bugs. CleanState executes all steps in a single call until it hits a blocking condition (event, time, predicate) or completes. Idle machines cost nothing — the scheduler skips them entirely.

---

## How does recovery work without replaying execution?

CleanState recovers from *domain truth*, not FSM position. A `MachineSnapshot` captures the checkpoint state and the context data that matters (scores, picks, counters — whatever you specify). On restore, a new machine is created from the same definition, the domain data is injected into its context, and execution resumes from the checkpoint state. No replay needed.

---

## Can multiple machines share one definition?

Yes. `MachineDefinition` is immutable and contains no per-instance state. You can create any number of `Machine` instances from a single definition. Each machine has its own `MachineContext`, step index, and status. All step types are stateless — timing state for `WaitForTime` is stored in the machine's context, not in the step object.

---

## How do state regions differ from running multiple machines?

State regions use `CompositeStateMachine` to coordinate multiple machines as *orthogonal concerns of a single entity* (e.g., a player's locomotion, posture, and weapon). The coordinator:

- Runs all regions under one scheduler
- Syncs each region's current state into every other region's context
- Evaluates cross-region constraints after each update (e.g., sprinting cancels aiming)
- Exposes an aggregate state tuple: `{ Locomotion: Running, Posture: Crouched, Weapon: Aiming }`

Running multiple machines independently (like the ParallelSidecar sample) is for *separate concerns that happen to coexist* — a main flow alongside a hint system and a watchdog. No shared state, no constraints, independent lifecycles.

---

## Why typed IDs instead of strings or enums?

- **Strings** require comparison at runtime — slow in hot paths
- **Enums** are rigid — adding a state means changing a shared enum, and IDs leak globally
- **Typed int-backed structs** (`StateId`, `EventId`, `MachineId`) give you compile-time safety (can't accidentally pass a `StateId` where an `EventId` is expected), O(1) lookup performance, and scoped identity (IDs are meaningful only within their machine definition). A `NameLookup` table provides human-readable names for debugging.

---

## How does the debug system avoid affecting production behavior?

The debugger observes through `IFsmObservable` — a read-only interface that exposes status, current state, and snapshots but no mutation methods. Debug commands (pause, resume, step, jump, breakpoints) go through an explicit opt-in `FsmDebugController` that the machine owner must create. The Unity editor layer never gets a raw `Machine` reference. If no debug controller exists, there is zero debug overhead.
