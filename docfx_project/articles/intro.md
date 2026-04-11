# Introduction

CleanState is a **deterministic orchestration engine** for complex systems where execution must be predictable, transitions must be traceable, systems must be recoverable, and performance must be tight.

## What Problem Does CleanState Solve?

Most FSM implementations work fine at small scale — until they don't. Common problems include:

### Frame-Driven Execution

```csharp
void Update()
{
    currentState.Update();
}
```

Logic runs every frame — even when nothing is happening. Hidden dependencies emerge between states. Wasted CPU, hard-to-reason execution order, subtle bugs.

### Coroutine-Based Flow

```csharp
yield return WaitUntil(() => conditionMet);
yield return WaitForSeconds(1.0f);
```

Execution is split across multiple frames with no explicit control flow. State is hidden inside Unity's coroutine system. Hard to trace, impossible to recover mid-flow.

### Opaque Debugging

When something breaks, you don't know which step failed, why a transition happened, or what triggered it.

### Tight Engine Coupling

Typical FSMs depend on MonoBehaviours, Update loops, and coroutines. Not portable, hard to test, hard to reuse.

### No Recovery Model

Most systems assume execution never stops. Real systems don't.

## How CleanState Is Different

| Problem | CleanState Solution |
|---|---|
| Frame-driven waste | **Run-until-blocked** — sleeps when idle, zero cost |
| Hidden execution | **Explicit step pipelines** — every step has a name, type, source location |
| Opaque transitions | **Full transition provenance** — reason, trigger, source, destination, timestamp |
| Engine coupling | **Pure C# core** — no MonoBehaviour, no coroutines, no Unity types |
| No recovery | **Checkpoint-based recovery** — restore from domain truth |
| Can't debug | **Visual debugger** — live state, timeline, breakpoints |

## Target Audience

CleanState is designed for:

- **Gameplay engineers** building complex, state-heavy systems
- **Tools engineers** who need deterministic, testable orchestration
- **Teams** who need their state machines to be **understood, not guessed**
