# Pick Game

A slot-style pick game — CleanState's flagship sample demonstrating complex gameplay orchestration.

## The Problem

Typical pick game implementations become a mess of scattered state:

```csharp
bool isRevealing;
bool hasMorePicks;
int picksRemaining;
IEnumerator RevealCoroutine() { ... }
```

Scattered state, hidden flow, impossible to trace or recover.

## What This Sample Shows

- **Branching logic** — three-way decision after each reveal (game over / no picks / continue)
- **Repeated loops** — pick → reveal → check → repeat
- **Recovery checkpoints** — stable points at Setup, AwaitPick, and Summary
- **Debug breakpoints** — breakpoint on Reveal state entry, inspected and resumed
- **Transition traceability** — full 12-transition timeline at the end

## State Flow

```
Setup → AwaitPick → Reveal ──┬── GameOver → Done
            ↑        │       │
            └────────┘       ├── Summary → Done
                             │
                             └── AwaitPick (loop)
```

## The Machine Definition

The entire game flow is defined as a single readable pipeline:

[!code-csharp[Machine Definition](../../samples/PickGame/Program.cs#L54-L165 "Pick Game machine definition")]

## Key Moments

### Breakpoint Hit

The sample sets a breakpoint on the `Reveal` state. When the machine enters Reveal for the first time, execution pauses:

```text
[BREAKPOINT ] Hit breakpoint: StateEntry on state 'Reveal'
[BREAKPOINT ]   Status: Blocked | Step: 0 (RevealResult)
[BREAKPOINT ]   Breakpoint disabled. Resuming...
```

This demonstrates that CleanState treats FSM execution like debuggable code — you can pause, inspect, and resume.

### Decision Branching

After each reveal, a three-way decision determines what happens next:

```csharp
.Decision("RevealDecision")
    .When(ctx => ctx.Get<bool>("gameOver"), "GameOver", "HitGameOver")
    .When(ctx => ctx.Get<int>("picksRemaining") <= 0, "Summary", "NoPicks")
    .Otherwise("AwaitPick", "ContinuePicking")
```

Every branch is named. The transition log shows exactly which branch was taken and why.

### Timeline Replay

At the end, the full transition history is printed:

```text
[1] Setup → AwaitPick       (Direct: GoToAwaitPick) at t=0.0s
[2] AwaitPick → Reveal      (Direct: GoToReveal) at t=0.1s
[3] Reveal → AwaitPick      (DecisionBranch: RevealDecision) at t=0.7s
...
[12] Summary → Done         (Direct: GoToDone) at t=4.4s
```

Every transition has a reason, a detail, and a timestamp.

## Running It

```bash
dotnet run --project samples/PickGame/PickGame.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/PickGame/Program.cs "Pick Game — full source")]
