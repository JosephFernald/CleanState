# Parallel Sidecar

Three machines running concurrently on a shared scheduler — a main game round with two independent sidecar machines for hints and timeout enforcement.

## The Problem

Running parallel concerns alongside a main flow typically means scattered Update loops and shared booleans:

```csharp
float hintTimer;
float watchdogTimer;
bool isPlaying;
bool hintShown;

void Update() {
    if (isPlaying && Time.time > hintTimer) { ShowHint(); ... }
    if (isPlaying && Time.time > watchdogTimer) { ForceEnd(); ... }
}
```

Coupled, fragile, untraceable.

## What This Sample Shows

- **Three independent machines** sharing one scheduler
- **Main flow**: Countdown → Play → ScoreScreen
- **Hint sidecar**: delivers timed hints during the Play phase
- **Watchdog sidecar**: enforces an 8-second time limit, force-ends the round
- **Cross-machine events**: main flow broadcasts `PlayStarted` to activate sidecars
- **Independent trace buffers**: each machine has its own transition history
- **Zero coupling**: machines don't share state — they communicate via events

## Architecture

```
Scheduler.Update(time)
    │
    ├── GameRound:      Countdown → Play ──────────────── → ScoreScreen
    │                              ↑ PlayStarted            ↑ RoundEnd
    │                              │                        │
    ├── HintSystem:     WaitForStart → Hint1 → Hint2 → Hint3 → Done
    │                              │
    └── TimeoutWatchdog: WaitForStart → Ticking → Ticking → ... → Expired
                                                                    │
                                                              (forces RoundEnd)
```

## Key Moments

### Concurrent Execution

All three machines tick on every `scheduler.Update()` call. The output interleaves naturally:

```text
[MAIN ] GO!
[HINT ] Tip: Collect the golden items for bonus points!
[WATCH] Timer started — 8.0s time limit.
[SCORE] +150 points! (total: 150)
[WATCH] 6s remaining
[HINT ] Tip: Watch for the multiplier power-up!
[SCORE] +250 points! (total: 400)
[WATCH] 4s remaining
```

### Cross-Machine Communication

The main flow broadcasts an event that activates both sidecars:

```csharp
hintMachine.SendEvent(playStartedHint, time);
watchdogMachine.SendEvent(playStartedWatch, time);
```

### Watchdog Force-End

When the watchdog expires, it triggers the main flow's `RoundEnd` event:

```text
[WATCH] WARNING: 0s remaining!
[WATCH] TIME'S UP! Forcing round end.
[MAIN ] Round ended.
```

### Independent Timelines

Each machine has its own trace buffer with its own history:

```text
MAIN FLOW:
  [1] Countdown → Play       at t=3.1s
  [2] Play → ScoreScreen     at t=11.3s

HINT SIDECAR:
  [1] WaitForStart → ShowHint1  at t=3.1s
  [2] ShowHint1 → ShowHint2     at t=5.7s
  [3] ShowHint2 → ShowHint3     at t=8.3s
  [4] ShowHint3 → HintsDone     at t=10.3s

WATCHDOG:
  [1] WaitForStart → Ticking  at t=3.1s
  [2] Ticking → Ticking       at t=5.2s
  ...
  [5] Ticking → Expired       at t=11.3s
```

## Running It

```bash
dotnet run --project samples/ParallelSidecar/ParallelSidecar.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/ParallelSidecar/Program.cs "Parallel Sidecar — full source")]
