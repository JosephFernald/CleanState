# ParallelSidecar

Three machines running concurrently on a shared scheduler — a main game round with hint system and timeout watchdog as independent sidecars.

## What This Demonstrates

- Three independent machines sharing one scheduler
- Main flow: Countdown → Play → ScoreScreen
- Hint sidecar: delivers timed hints during the Play phase
- Watchdog sidecar: enforces an 8-second time limit, force-ends the round
- Cross-machine event communication (`PlayStarted` broadcast)
- Independent trace buffers per machine
- Zero coupling — no shared booleans, no coupled Update loops

## Problem It Solves

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

Coupled, fragile, untraceable. Adding a new concern means touching existing code. Removing one means hoping nothing else depended on its booleans.

CleanState gives each concern its own machine with its own lifecycle. The scheduler drives them all. No coupling.

## How to Run

```bash
dotnet run --project samples/ParallelSidecar/ParallelSidecar.csproj
```

## What to Look For

- **Interleaved output** — `[MAIN]`, `[HINT]`, `[WATCH]`, and `[SCORE]` lines interleave naturally as all three machines tick
- **Sidecar activation** — hints and watchdog both start when the main flow broadcasts `PlayStarted`
- **Countdown** — 3... 2... 1... GO! with timed waits between each
- **Hint delivery** — three tips appear at intervals during gameplay
- **Watchdog ticking** — remaining time counts down: `6s`, `4s`, `WARNING: 2s`, `WARNING: 0s`
- **Force-end** — watchdog expiring triggers `RoundEnd` on the main flow
- **Score screen** — shows final score and `Ended by: watchdog`
- **Three separate timelines** — each machine's transition history printed independently
