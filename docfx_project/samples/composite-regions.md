# Composite Regions

Three orthogonal state machines modeling a player character — locomotion, posture, and weapon — composed through `CompositeStateMachine` with cross-region constraints.

## The Problem

A single FSM for a player character explodes combinatorially:

```
Idle, Walking, Running,
IdleCrouched, WalkingCrouched, RunningCrouched,
IdleAiming, WalkingAiming, RunningAiming,
IdleCrouchedAiming, WalkingCrouchedAiming, ...
```

Every new concern multiplies the state count. Adding a fourth concern multiplies again.

## What This Sample Shows

- **Orthogonal state composition** — 3 regions, 8 total states instead of 18+
- **`CompositeStateMachine`** coordinating independent machines under one scheduler
- **Aggregate state tuple** — `{ Locomotion: Running, Posture: Crouched, Weapon: Aiming }`
- **Cross-region state reads** — each machine can read other regions' state via `__region.{name}` context keys
- **Constraint enforcement** — running forces standing posture
- **Independent trace buffers** — each region has its own transition history

## Architecture

```
CompositeStateMachine("PlayerState")
    │
    ├── Locomotion:  Idle ↔ Walking ↔ Running
    │
    ├── Posture:     Standing ↔ Crouched
    │                   ↑
    │                   └── forced by Running constraint
    │
    └── Weapon:      HipFire ↔ Aiming
                        ↑         ↓
                        └── Reloading (timed, auto-return)
```

## Key Moments

### Aggregate State Tuple

After each action, the full state is printed as a tuple:

```text
[STATE] { Locomotion: Walking, Posture: Crouched, Weapon: Aiming }
```

### Cross-Region Constraint

When the player starts running while crouched:

```text
[INPUT] Player starts running (will force standing!)
[LOCO ] Running!
[RULE ] Running forces standing posture!
[POST ] Standing upright
[STATE] { Locomotion: Running, Posture: Standing, Weapon: Aiming }
```

### Independent Timelines

Each region's transitions are tracked separately:

```text
LOCOMOTION:
  [1] Idle → Walking     (Direct: GoToWalking)
  [2] Walking → Running  (DecisionBranch: WalkDecision)
  [3] Running → Walking  (DecisionBranch: RunDecision)
  [4] Walking → Idle     (DecisionBranch: WalkDecision)

POSTURE:
  [1] Standing → Crouched  (Direct: GoToCrouched)
  [2] Crouched → Standing  (Direct: GoToStanding)
  [3] Standing → Crouched  (Direct: GoToCrouched)

WEAPON:
  [1] HipFire → Aiming    (DecisionBranch: HipDecision)
  [2] Aiming → Reloading  (DecisionBranch: AimDecision)
  [3] Reloading → HipFire (Direct: BackToHipFire)
  [4] HipFire → Aiming    (DecisionBranch: HipDecision)
```

## The CompositeStateMachine API

```csharp
// Create the composite
var player = new CompositeStateMachine("PlayerState");

// Add regions
player.AddRegion("Locomotion", locomotionDef);
player.AddRegion("Posture", postureDef);
player.AddRegion("Weapon", weaponDef);

// Add cross-region constraints
player.AddConstraint((composite, time) =>
{
    if (composite.GetRegionState("Locomotion") == "Running"
        && composite.GetRegionState("Posture") == "Crouched")
    {
        composite.SendEvent("Posture", "Stand", time);
    }
});

// Run
player.Start(time);
player.Update(time);

// Get aggregate state
var snapshot = player.GetSnapshot();
// snapshot.GetRegionState("Locomotion") => "Running"
```

## Running It

```bash
dotnet run --project samples/CompositeRegions/CompositeRegions.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/CompositeRegions/Program.cs "Composite Regions — full source")]
