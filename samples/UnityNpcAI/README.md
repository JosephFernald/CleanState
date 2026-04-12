# UnityNpcAI

An enemy guard NPC with patrol, alert, chase, attack, search, and death states — the classic Unity AI pattern rebuilt with CleanState.

> This runs as a console app simulating Unity's game loop. The same machine definition works unchanged in Unity 6 with `Scheduler.Update(Time.time)` in a MonoBehaviour.

## What This Demonstrates

- 6-state NPC AI: Patrol → Alert → Chase → Attack → Search → Dead
- Waypoint-based patrol loop with timed movement
- Alert phase with threat assessment before committing to chase
- Distance-based and visibility-based transitions
- Attack with cooldown, re-evaluation after each strike
- Search behavior when player is lost, then return to patrol
- Full lifecycle from spawn to death with transition tracing

## Problem It Solves

The typical Unity NPC AI:

```csharp
public class EnemyAI : MonoBehaviour
{
    enum State { Patrol, Chase, Attack, Search, Dead }
    State currentState;
    float stateTimer;
    int waypointIndex;
    bool playerInRange;
    bool playerVisible;
    bool attackCooldown;

    void Update()
    {
        switch (currentState)
        {
            case State.Patrol:
                MoveToWaypoint();
                if (CanSeePlayer()) { currentState = State.Chase; }
                break;
            case State.Chase:
                MoveToPlayer();
                if (InAttackRange()) { currentState = State.Attack; }
                if (!CanSeePlayer()) { currentState = State.Search; }
                break;
            // ... grows with every new behavior
        }
    }
}
```

No traceability. Every new behavior adds another case. Transitions are invisible. Debugging is "add a Debug.Log and hope."

## Unity Integration Pattern

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
        _machine.Start(Time.time);
    }

    void Update()
    {
        _scheduler.Update(Time.time);
    }

    // Send events from game systems (triggers, raycasts, etc.):
    public void OnPlayerSpotted() =>
        _machine.SendEvent(playerSpottedId, Time.time);
}
```

## How to Run

```bash
dotnet run --project samples/UnityNpcAI/UnityNpcAI.csproj
```

## What to Look For

- **Patrol loop** — NPC walks between waypoints (Gate → Tower → Bridge → Courtyard) on a timer
- **Alert phase** — NPC doesn't immediately chase, pauses to assess the threat first
- **Chase with distance tracking** — watch the distance decrease as the player approaches
- **Attack + cooldown** — NPC strikes, waits 1 second, then re-evaluates
- **Player retreats** — NPC transitions back to Chase when player leaves melee range
- **Lost visual** — NPC enters Search, looks around for 3 seconds, then returns to Patrol
- **Final encounter** — player flanks the NPC: Alert → Chase → Attack → Dead in rapid succession
- **Timeline** — 26 transitions with full provenance, every decision labeled
