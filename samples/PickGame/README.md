# PickGame

CleanState's flagship sample — a slot-style pick game with looping, branching, and full debugging.

## What This Demonstrates

- Repeated state loops (`AwaitPick → Reveal → decision → repeat`)
- Three-way decision branching (game over / no picks left / continue)
- Event-driven progression (`PlayerPicked`)
- Timed waits (`RevealPause`, `SummaryPause`)
- Recovery checkpoints at stable boundaries
- Debug breakpoint on Reveal state entry — hit, inspected, disabled, resumed
- Full 12-transition timeline replay

## Problem It Solves

Typical pick game implementations scatter state across booleans and coroutines:

```csharp
bool isRevealing;
bool hasMorePicks;
int picksRemaining;
IEnumerator RevealCoroutine() { ... }
```

Hidden flow, impossible to trace, impossible to recover. When something breaks, you don't know which step failed or why a transition happened.

CleanState replaces all of this with a single readable pipeline where every step is named and every transition has a reason.

## How to Run

```bash
dotnet run --project samples/PickGame/PickGame.csproj
```

## What to Look For

- **Breakpoint hit** — execution pauses on first entry to Reveal, shows the snapshot, then resumes
- **Decision branching** — watch the transition log show `DecisionBranch: RevealDecision` with the specific branch label
- **Grid visualization** — revealed cells show prizes (`$250`) or `XX` (game over), unrevealed show `??`
- **Timeline at the end** — every transition with reason, detail, and timestamp
- **Final snapshot** — machine name, status, state, and transition count
