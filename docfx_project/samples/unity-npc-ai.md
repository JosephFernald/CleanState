# Unity NPC AI

An enemy guard NPC with patrol, alert, chase, attack, search, and death states — the classic Unity AI pattern rebuilt with CleanState. Runs as a console app simulating Unity's game loop; the same definition works unchanged in Unity 6.

## The Problem

The typical Unity NPC AI lives in a `switch` statement inside `Update()`:

```csharp
void Update()
{
    switch (currentState)
    {
        case State.Patrol:
            MoveToWaypoint();
            if (CanSeePlayer()) currentState = State.Chase;
            break;
        case State.Chase:
            MoveToPlayer();
            if (InAttackRange()) currentState = State.Attack;
            if (!CanSeePlayer()) currentState = State.Search;
            break;
        // ... grows with every new behavior
    }
}
```

No traceability. Every new behavior adds another case. Transitions are invisible. Debugging is "add a Debug.Log and hope."

## What This Sample Shows

- **6-state NPC AI**: Patrol, Alert, Chase, Attack, Search, Dead
- **Waypoint patrol** with timed movement between named locations
- **Alert phase** — NPC doesn't immediately chase, pauses to assess the threat
- **Chase** with 0.5s polling interval and distance tracking
- **Attack** with cooldown and post-strike re-evaluation
- **Search** — looks around for 3 seconds, then returns to patrol
- **Full lifecycle** from spawn to death with 26 traced transitions

## State Flow

```
Patrol ──→ Alert ──→ Chase ──→ Attack
  ↑          │         ↑         │
  │          │         └─────────┘ (still in range)
  │          │         │
  │          └→ Patrol │ (false alarm)
  │                    │
  └──── Search ←───────┘ (lost visual)
                         
         Attack ──→ Dead (NPC health <= 0)
```

## Unity Integration

The same CleanState definition runs in Unity with minimal wiring:

```csharp
public class EnemyController : MonoBehaviour
{
    private Scheduler _scheduler;
    private Machine _machine;

    void Start()
    {
        _scheduler = new Scheduler();
        _machine = _scheduler.CreateMachine(BuildAIDefinition());
        _machine.Start(Time.timeAsDouble);
    }

    void Update()
    {
        _scheduler.Update(Time.timeAsDouble);
    }

    public void OnPlayerSpotted() =>
        _machine.SendEvent(playerSpottedId, Time.timeAsDouble);
}
```

## Key Moments

### Alert Phase

The NPC doesn't immediately chase — it pauses for 1.5 seconds to assess:

```text
[PATROL] Reached waypoint. Next: Gate
Patrol → Alert | DecisionBranch: PatrolCheck
[ALERT ] Suspicious! Raising weapon...
[ALERT ] Assessing threat...
Alert → Chase | DecisionBranch: AlertDecision
[CHASE ] Hostile detected! Pursuing!
```

### Attack and Retreat

NPC attacks, player dodges, NPC transitions back to chase:

```text
Chase → Attack | DecisionBranch: ChaseDecision
[ATTACK] ATTACKING! Dealt 25 damage!
[ATTACK] Attack cooldown finished.
Attack → Chase | DecisionBranch: PostAttackDecision
[CHASE ] Hostile detected! Pursuing!
```

### Search and Return

Player hides, NPC searches, gives up, returns to patrol:

```text
Chase → Search | DecisionBranch: ChaseDecision
[SEARCH] Lost visual. Searching area...
[SEARCH] Checking left...
[SEARCH] Checking right...
Search → Patrol | DecisionBranch: SearchResult
[PATROL] Patrolling to waypoint: Gate
```

### Final Timeline

Every transition is traceable with reason and timestamp:

```text
[ 4] Patrol → Alert    (DecisionBranch: PatrolCheck) at t=8.3s
[ 6] Alert → Chase     (DecisionBranch: AlertDecision) at t=11.3s
[14] Chase → Attack    (DecisionBranch: ChaseDecision) at t=15.3s
[16] Attack → Chase    (DecisionBranch: PostAttackDecision) at t=17.3s
[20] Chase → Search    (DecisionBranch: ChaseDecision) at t=19.3s
[21] Search → Patrol   (DecisionBranch: SearchResult) at t=22.3s
[26] Attack → Dead     (DecisionBranch: PostAttackDecision) at t=29.3s
```

## Running It

```bash
dotnet run --project samples/UnityNpcAI/UnityNpcAI.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/UnityNpcAI/Program.cs "Unity NPC AI — full source")]
