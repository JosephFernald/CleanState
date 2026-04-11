# StateRegionsPlayer

A single machine is best for one concern. Some systems have multiple simultaneous concerns. This sample composes three machines to model one player without state explosion.

> **When to use state regions:** Use this pattern when behaviors are simultaneous but independent, such as movement, posture, and weapon handling.

## Why Not One Machine?

A player can be Running, Crouched, and Aiming — all at the same time. A single FSM must represent every combination as a discrete state:

```
IdleStandingHipFire         WalkingStandingHipFire
IdleStandingAiming          WalkingStandingAiming
IdleStandingReloading       WalkingStandingReloading
IdleCrouchedHipFire         WalkingCrouchedHipFire
IdleCrouchedAiming          WalkingCrouchedAiming
RunningStandingHipFire      SprintingStandingHipFire
RunningCrouchedAiming       SprintingCrouchedReloading
...
```

4 locomotion x 3 posture x 3 weapon = **36 states minimum**. Add one more concern (e.g., NetworkAuthority with 4 states) and it becomes **144 states**.

## The State Regions Approach

Three independent machines, each modeling one concern:

| Region | States | Count |
|---|---|---:|
| Locomotion | Idle, Walking, Running, Sprinting | 4 |
| Posture | Standing, Crouched, Prone | 3 |
| Weapon | HipFire, Aiming, Reloading | 3 |
| **Total** | | **10** |

The aggregate player state is the tuple of all three:

```
{ Locomotion: Running, Posture: Crouched, Weapon: Aiming }
```

## What This Demonstrates

- **State explosion avoided** — 10 states instead of 36+
- **Cross-region constraints** that enforce coherent behavior:
  - Sprinting cancels aiming (weapon forced to HipFire)
  - Reloading blocks aiming (weapon locked until done)
  - Going prone cancels sprint (locomotion forced to Walking)
  - Cross-region constraints are enforced by the coordinator layer, not by merging regions into one machine
- **Aggregate debug view** — full state tuple printed after every action
- **Per-region transition tracing** — each region has independent history
- **Timed state** — Reloading runs for 2 seconds, then auto-returns to HipFire

## How to Run

```bash
dotnet run --project samples/StateRegionsPlayer/StateRegionsPlayer.csproj
```

## What to Look For

- **Sprint cancels aim** — when the player sprints while aiming, watch `[RULE]` fire and force the weapon to HipFire
- **Reload blocks aim** — when the player tries to aim during reload, watch `[BLOCK]` deny the action
- **Reload auto-completes** — the weapon stays in Reloading for 2 seconds, then returns to HipFire without any event
- **State tuple after each action** — `[STATE]` shows all three regions as one coherent view
- **State count comparison** — the final summary shows 10 vs 36 (and 14 vs 144 with a fourth concern)
- **Independent timelines** — each region's transitions are traced separately
