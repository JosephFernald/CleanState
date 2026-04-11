# CompositeRegions

Three orthogonal state machines modeling a player character — locomotion, posture, and weapon — composed through CleanState's `CompositeStateMachine` with cross-region constraints.

## What This Demonstrates

- Orthogonal state composition (3 regions, 8 total states instead of 18+)
- `CompositeStateMachine` coordinating independent machines
- Aggregate state tuple: `{ Locomotion: Running, Posture: Crouched, Weapon: Aiming }`
- Cross-region state reads via `__region.{name}` context keys
- Constraint enforcement: running forces standing posture
- Independent trace buffers per region
- Each machine retains all core guarantees (single active state, transition provenance, recovery)

## Problem It Solves

A monolithic FSM for a player character explodes combinatorially:

```
Idle, Walking, Running,
IdleCrouched, WalkingCrouched, RunningCrouched,
IdleAiming, WalkingAiming, RunningAiming,
IdleCrouchedAiming, WalkingCrouchedAiming, RunningCrouchedAiming,
IdleReloading, WalkingReloading, RunningReloading,
...
```

Every new concern multiplies the state count. Adding a fourth concern (e.g., NetworkAuthority) multiplies again. Unmanageable.

With orthogonal regions, you add a new machine — not new states to every existing combination.

## How to Run

```bash
dotnet run --project samples/CompositeRegions/CompositeRegions.csproj
```

## What to Look For

- **State tuple** — after each action, the `[STATE]` line shows all three regions as a single tuple
- **Cross-region constraint** — when the player starts running while crouched, watch `[RULE]` fire and force standing posture
- **Independent transitions** — each region transitions on its own terms, not triggered by other regions
- **Reload timing** — weapon enters Reloading for 2 seconds, then automatically returns to HipFire
- **Three timelines** — each region's transition history is independent and separately traceable
- **Final count** — 3 regions, 8 states total, vs. 18+ for a monolithic approach
