// =============================================================================
// CleanState Sample: Unity NPC AI (Enemy Guard)
// =============================================================================
//
// This sample demonstrates how to build NPC AI with CleanState in Unity 6.
// It runs as a console app simulating Unity's game loop, but the structure
// maps directly to MonoBehaviour + FsmRunner integration.
//
// ── The typical Unity approach ──
//
//   public class EnemyAI : MonoBehaviour
//   {
//       enum State { Patrol, Chase, Attack, Search, Dead }
//       State currentState;
//       float stateTimer;
//       int waypointIndex;
//       bool playerInRange;
//       bool playerVisible;
//       bool attackCooldown;
//
//       void Update()
//       {
//           switch (currentState)
//           {
//               case State.Patrol:
//                   MoveToWaypoint();
//                   if (CanSeePlayer()) { currentState = State.Chase; }
//                   break;
//               case State.Chase:
//                   MoveToPlayer();
//                   if (InAttackRange()) { currentState = State.Attack; }
//                   if (!CanSeePlayer()) { currentState = State.Search; }
//                   break;
//               // ... dozens more lines, growing with every feature
//           }
//       }
//   }
//
//   No traceability. Every new behavior adds another case. Transitions are
//   invisible. Debugging is "add a Debug.Log and hope."
//
// ── The CleanState approach ──
//
//   Each behavior is a named state with explicit steps.
//   Transitions have reasons. The debugger shows exactly where the NPC is.
//   Adding a new behavior means adding a state, not touching existing code.
//
// ── Unity integration pattern ──
//
//   // On a MonoBehaviour:
//   public class EnemyController : MonoBehaviour
//   {
//       private Scheduler _scheduler;
//       private Machine _machine;
//
//       void Start()
//       {
//           _scheduler = new Scheduler();
//           _machine = _scheduler.CreateMachine(BuildAIDefinition());
//           _machine.Start(Time.time);
//       }
//
//       void Update()
//       {
//           _scheduler.Update(Time.time);
//       }
//
//       // Send events from game systems:
//       public void OnPlayerSpotted() =>
//           _machine.SendEvent(playerSpottedId, Time.time);
//   }
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.UnityNpcAI;

/// <summary>
/// Demonstrates a 6-state NPC AI (Patrol, Alert, Chase, Attack, Search, Dead)
/// using CleanState, simulating a Unity game loop. The same MachineDefinition
/// runs unchanged in Unity 6 with Scheduler.Update(Time.time).
/// </summary>
class Program
{
    // ── Simulated world state ──
    // In Unity, these would be real transforms, raycasts, and Physics.OverlapSphere.
    // Here they're mutated by a scripted timeline to drive the NPC through its behaviors.

    /// <summary>Distance from the NPC to the player in meters.</summary>
    static float _playerDistance = 20f;
    /// <summary>Whether the NPC currently has line-of-sight to the player.</summary>
    static bool _playerVisible = false;
    /// <summary>NPC health. When this reaches 0, the NPC transitions to Dead.</summary>
    static float _npcHealth = 100f;
    /// <summary>Current waypoint index in the patrol route.</summary>
    static int _waypointIndex = 0;
    /// <summary>Named patrol waypoints the NPC cycles through.</summary>
    static readonly string[] Waypoints = { "Gate", "Tower", "Bridge", "Courtyard" };

    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Unity NPC AI (Enemy Guard)    ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  States: Patrol, Alert, Chase, Attack, Search, Dead ║");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  Simulates a Unity game loop with Time.time,        ║");
        Console.WriteLine("║  raycasts, and distance checks replaced by          ║");
        Console.WriteLine("║  scripted events.                                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build the NPC AI ──

        var definition = new MachineBuilder("EnemyGuard")

            // ── PATROL ──
            // Walk between waypoints on a 2-second timer. After each waypoint,
            // check if the player is visible and within 15m. If so, transition
            // to Alert. Otherwise, loop back and walk to the next waypoint.
            // Marked as a checkpoint for recovery.
            .State("Patrol")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var wp = Waypoints[_waypointIndex];
                    Print("PATROL", $"Patrolling to waypoint: {wp}", ConsoleColor.Gray);
                    ctx.Set("alertLevel", 0f);
                }, "BeginPatrol")
                .WaitForTime(2.0f, "WalkToWaypoint")
                .Then(ctx =>
                {
                    _waypointIndex = (_waypointIndex + 1) % Waypoints.Length;
                    var wp = Waypoints[_waypointIndex];
                    Print("PATROL", $"Reached waypoint. Next: {wp}", ConsoleColor.Gray);
                }, "ArriveAtWaypoint")
                .Decision("PatrolCheck")
                    .When(ctx => _playerVisible && _playerDistance < 15f, "Alert", "PlayerSpotted")
                    .Otherwise("Patrol", "ContinuePatrol")

            // ── ALERT ──
            // NPC spotted something suspicious. Raises weapon and pauses for
            // 1.5 seconds to assess the threat. If the player is confirmed
            // visible within 12m, transitions to Chase. If the player
            // disappears, returns to Patrol (false alarm).
            .State("Alert")
                .TransitionIn(ctx =>
                {
                    Print("ALERT", "Suspicious! Raising weapon...", ConsoleColor.Yellow);
                    ctx.Set("alertLevel", 0.5f);
                }, "RaiseAlert")
                .WaitForTime(1.5f, "AssessThreat")
                .Then(ctx =>
                {
                    Print("ALERT", "Assessing threat...", ConsoleColor.Yellow);
                }, "CheckThreat")
                .Decision("AlertDecision")
                    .When(ctx => _playerVisible && _playerDistance < 12f, "Chase", "ThreatConfirmed")
                    .When(ctx => !_playerVisible, "Patrol", "FalseAlarm")
                    .Otherwise("Alert", "StillAssessing")

            // ── CHASE ──
            // Actively pursuing the player. Re-evaluates every 0.5 seconds
            // (using WaitForTime, not per-frame polling). Transitions to:
            //   Attack — if player is within 3m (melee range)
            //   Search — if line-of-sight is lost or player exceeds 25m
            .State("Chase")
                .TransitionIn(ctx =>
                {
                    Print("CHASE", "Hostile detected! Pursuing!", ConsoleColor.Red);
                    ctx.Set("alertLevel", 1f);
                }, "BeginChase")
                .WaitForTime(0.5f, "ChaseTickInterval")
                .Then(ctx =>
                {
                    Print("CHASE", $"Chasing... distance: {_playerDistance:F1}m", ConsoleColor.Red);
                }, "UpdateChase")
                .Decision("ChaseDecision")
                    .When(ctx => _playerDistance <= 3f, "Attack", "InAttackRange")
                    .When(ctx => !_playerVisible, "Search", "LostVisual")
                    .When(ctx => _playerDistance > 25f, "Search", "TooFarAway")
                    .Otherwise("Chase", "StillChasing")

            // ── ATTACK ──
            // In melee range. Deals damage, then waits 1 second (attack
            // cooldown). After cooldown, re-evaluates:
            //   Dead     — if NPC health has been reduced to 0
            //   Attack   — if player is still within 3m
            //   Chase    — if player retreated but is still visible
            //   Search   — if player disappeared after the attack
            .State("Attack")
                .TransitionIn(ctx =>
                {
                    var dmg = 25;
                    Print("ATTACK", $"ATTACKING! Dealt {dmg} damage!", ConsoleColor.Magenta);
                    ctx.Set("lastAttackDamage", dmg);
                }, "StrikePlayer")
                .WaitForTime(1.0f, "AttackCooldown")
                .Then(ctx =>
                {
                    Print("ATTACK", "Attack cooldown finished.", ConsoleColor.Magenta);
                }, "CooldownDone")
                .Decision("PostAttackDecision")
                    .When(ctx => _npcHealth <= 0f, "Dead", "NpcKilled")
                    .When(ctx => _playerDistance <= 3f, "Attack", "StillInRange")
                    .When(ctx => _playerVisible, "Chase", "PlayerRetreated")
                    .Otherwise("Search", "LostAfterAttack")

            // ── SEARCH ──
            // Lost line-of-sight to the player. Looks left (1.5s) then
            // right (1.5s). If the player reappears within 15m during
            // the search, transitions back to Chase. Otherwise, gives
            // up and returns to Patrol.
            .State("Search")
                .TransitionIn(ctx =>
                {
                    Print("SEARCH", "Lost visual. Searching area...", ConsoleColor.DarkYellow);
                    ctx.Set("searchStartTime", ctx.CurrentTime);
                }, "BeginSearch")
                .WaitForTime(1.5f, "LookAround1")
                .Then(ctx =>
                {
                    Print("SEARCH", "Checking left...", ConsoleColor.DarkYellow);
                }, "ScanLeft")
                .WaitForTime(1.5f, "LookAround2")
                .Then(ctx =>
                {
                    Print("SEARCH", "Checking right...", ConsoleColor.DarkYellow);
                }, "ScanRight")
                .Decision("SearchResult")
                    .When(ctx => _playerVisible && _playerDistance < 15f, "Chase", "FoundPlayerAgain")
                    .Otherwise("Patrol", "GaveUp")

            // ── DEAD ──
            // Terminal state. NPC has been eliminated. Logs survival time.
            // Machine completes here (no outgoing transitions).
            .State("Dead")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("DEAD", "NPC eliminated.", ConsoleColor.DarkRed);
                    Print("DEAD", $"Survived for {ctx.CurrentTime:F1}s", ConsoleColor.DarkRed);
                }, "Die")

            .Build();

        // ── Set up scheduler and tracing ──
        // In Unity, the Scheduler lives on a MonoBehaviour and Update() calls
        // scheduler.Update(Time.time). The TraceBuffer is optional — attach it
        // when you want transition history for debugging.

        var traceBuffer = new TraceBuffer(64);
        var scheduler = new Scheduler();
        var machine = scheduler.CreateMachine(definition, traceBuffer);

        machine.OnTransition += trace =>
        {
            var from = definition.NameLookup.GetStateName(trace.FromState);
            var to = definition.NameLookup.GetStateName(trace.ToState);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  ── {from} → {to} | {trace.Reason}: {trace.Detail}");
            Console.ResetColor();
        };

        // ══════════════════════════════════════════════════════
        // Simulate a Unity game loop with scripted events
        // ══════════════════════════════════════════════════════

        Print("ENGINE", "Starting NPC AI (simulating Unity game loop)...");
        Print("ENGINE", "In Unity: Scheduler.Update(Time.time) in MonoBehaviour.Update()");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        machine.Start(time);

        // Script: a sequence of world events that drive the NPC.
        // In Unity, these would come from physics triggers, raycasts, and
        // game systems — not a predefined array. Here we simulate them
        // at specific times to demonstrate the full NPC lifecycle.
        var worldEvents = new (float atTime, string description, Action action)[]
        {
            // NPC patrols peacefully for a bit
            (5f,  "Player appears in the distance",
                () => { _playerVisible = true; _playerDistance = 18f; }),

            // Player gets closer — triggers alert
            (8f,  "Player moves closer (14m)",
                () => { _playerDistance = 14f; }),

            // Player confirmed — chase begins
            (11f, "Player clearly visible (10m)",
                () => { _playerDistance = 10f; }),

            // Chase updates
            (13f, "Player running (8m)",
                () => { _playerDistance = 8f; }),

            (14f, "Closing in (5m)",
                () => { _playerDistance = 5f; }),

            // In attack range
            (15f, "In melee range (2m)",
                () => { _playerDistance = 2f; }),

            // Player dodges, retreats
            (17f, "Player dodges back (7m)",
                () => { _playerDistance = 7f; }),

            // Player hides
            (19f, "Player hides behind cover",
                () => { _playerVisible = false; _playerDistance = 12f; }),

            // Player sneaks up and eliminates NPC
            (25f, "Player flanks and eliminates NPC",
                () => { _npcHealth = 0f; _playerVisible = true; _playerDistance = 2f; }),
        };

        int eventIndex = 0;

        while (machine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            // Fire world events at their scheduled times
            while (eventIndex < worldEvents.Length && time >= worldEvents[eventIndex].atTime)
            {
                var evt = worldEvents[eventIndex];
                Console.WriteLine();
                Print("WORLD", $"[t={evt.atTime:F0}s] {evt.description}", ConsoleColor.DarkGray);
                evt.action();
                eventIndex++;
            }

            scheduler.Update(time);

            // Safety: don't run forever
            if (time > 60f) break;
        }

        // ── Timeline ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("TIMELINE", "Full NPC behavior trace:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));

        var traces = traceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = definition.NameLookup.GetStateName(t.FromState);
            var to = definition.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1,2}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail}) at t={t.Timestamp:F1}s");
        }

        Console.WriteLine(new string('═', 60));

        // ── Summary ──

        Console.WriteLine();
        var snap = machine.GetDebugSnapshot();
        Print("RESULT", $"Machine: {snap.MachineName} — {snap.Status}");
        Print("RESULT", $"Final state: {snap.CurrentStateName}");
        Print("RESULT", $"Transitions: {traceBuffer.Count}");
        Console.WriteLine();
        Print("UNITY", "In Unity, this exact definition runs with:");
        Print("UNITY", "  var machine = scheduler.CreateMachine(definition);");
        Print("UNITY", "  machine.Start(Time.time);");
        Print("UNITY", "  // In Update(): scheduler.Update(Time.time);");
        Print("UNITY", "  // Send events from triggers: machine.SendEvent(id, Time.time);");
        Print("UNITY", "No switch statements. No Update() spaghetti. Full traceability.");
    }

    static void Print(string tag, string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{tag,-12}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
