// =============================================================================
// CleanState Sample: Composite State Regions
// =============================================================================
//
// This sample demonstrates orthogonal state composition — multiple independent
// state machines modeling different concerns of a single entity.
//
// ── The problem with monolithic FSMs ──
//
//   A player character in a shooter has locomotion, posture, and weapon states.
//   A single FSM explodes combinatorially:
//
//     Idle, Walking, Running,
//     IdleCrouched, WalkingCrouched, RunningCrouched,
//     IdleAiming, WalkingAiming, RunningAiming,
//     IdleCrouchedAiming, WalkingCrouchedAiming, RunningCrouchedAiming,
//     IdleReloading, WalkingReloading, RunningReloading,
//     ...
//
//   Every new concern multiplies the state count. Unmanageable.
//
// ── The CleanState approach ──
//
//   Three independent machines, each modeling one concern:
//     Locomotion: Idle, Walking, Running
//     Posture:    Standing, Crouched
//     Weapon:     HipFire, Aiming, Reloading
//
//   The aggregate state is inferred from the tuple:
//     { Locomotion: Running, Posture: Crouched, Weapon: Aiming }
//
//   Cross-region constraints enforce rules:
//     - Cannot aim while reloading
//     - Sprint forces standing posture
//
// =============================================================================

using System;
using System.Linq;
using CleanState.Builder;
using CleanState.Composition;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.CompositeRegions;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Composite State Regions       ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Three orthogonal state machines for a player:      ║");
        Console.WriteLine("║  - Locomotion: Idle, Walking, Running               ║");
        Console.WriteLine("║  - Posture: Standing, Crouched                      ║");
        Console.WriteLine("║  - Weapon: HipFire, Aiming, Reloading              ║");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  With cross-region constraints:                     ║");
        Console.WriteLine("║  - Cannot aim while reloading                       ║");
        Console.WriteLine("║  - Running forces standing posture                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ══════════════════════════════════════════════════════
        // Region 1: Locomotion
        // ══════════════════════════════════════════════════════

        var locomotionDef = new MachineBuilder("Locomotion")
            .State("Idle")
                .TransitionIn(ctx => Print("LOCO", "Standing still", ConsoleColor.White), "EnterIdle")
                .WaitForEvent("StartWalking", "WaitToWalk")
                .GoTo("Walking", "GoToWalking")
            .State("Walking")
                .TransitionIn(ctx => Print("LOCO", "Walking", ConsoleColor.White), "EnterWalking")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("WalkDecision")
                    .When(ctx => ctx.TryGet<string>("locoTarget", out var t) && t == "run", "Running", "StartRunning")
                    .When(ctx => ctx.TryGet<string>("locoTarget", out var t) && t == "stop", "Idle", "StopMoving")
                    .Otherwise("Walking", "KeepWalking")
            .State("Running")
                .TransitionIn(ctx => Print("LOCO", "Running!", ConsoleColor.Green), "EnterRunning")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("RunDecision")
                    .When(ctx => ctx.TryGet<string>("locoTarget", out var t) && t == "walk", "Walking", "SlowToWalk")
                    .When(ctx => ctx.TryGet<string>("locoTarget", out var t) && t == "stop", "Idle", "StopMoving")
                    .Otherwise("Running", "KeepRunning")
            .Build();

        // ══════════════════════════════════════════════════════
        // Region 2: Posture
        // ══════════════════════════════════════════════════════

        var postureDef = new MachineBuilder("Posture")
            .State("Standing")
                .TransitionIn(ctx => Print("POST", "Standing upright", ConsoleColor.White), "EnterStanding")
                .WaitForEvent("Crouch", "WaitToCrouch")
                .GoTo("Crouched", "GoToCrouched")
            .State("Crouched")
                .TransitionIn(ctx => Print("POST", "Crouched down", ConsoleColor.Yellow), "EnterCrouched")
                .WaitForEvent("Stand", "WaitToStand")
                .GoTo("Standing", "GoToStanding")
            .Build();

        // ══════════════════════════════════════════════════════
        // Region 3: Weapon
        // ══════════════════════════════════════════════════════

        var weaponDef = new MachineBuilder("Weapon")
            .State("HipFire")
                .TransitionIn(ctx => Print("WEAP", "Hip fire ready", ConsoleColor.White), "EnterHipFire")
                .WaitForEvent("WeaponCommand", "WaitForWeaponCmd")
                .Decision("HipDecision")
                    .When(ctx => ctx.TryGet<string>("weaponTarget", out var t) && t == "aim", "Aiming", "StartAiming")
                    .When(ctx => ctx.TryGet<string>("weaponTarget", out var t) && t == "reload", "Reloading", "StartReloading")
                    .Otherwise("HipFire", "StayHipFire")
            .State("Aiming")
                .TransitionIn(ctx =>
                {
                    // Check if reloading in progress (cross-region read)
                    var weaponState = ctx.TryGet<string>("__region.Weapon", out var ws) ? ws : "";
                    Print("WEAP", "Aiming down sights", ConsoleColor.Cyan);
                }, "EnterAiming")
                .WaitForEvent("WeaponCommand", "WaitForWeaponCmd")
                .Decision("AimDecision")
                    .When(ctx => ctx.TryGet<string>("weaponTarget", out var t) && t == "hipfire", "HipFire", "StopAiming")
                    .When(ctx => ctx.TryGet<string>("weaponTarget", out var t) && t == "reload", "Reloading", "AimToReload")
                    .Otherwise("Aiming", "StayAiming")
            .State("Reloading")
                .TransitionIn(ctx =>
                {
                    Print("WEAP", "Reloading...", ConsoleColor.Red);
                }, "EnterReloading")
                .WaitForTime(2.0f, "ReloadDuration")
                .Then(ctx =>
                {
                    Print("WEAP", "Reload complete!", ConsoleColor.Green);
                }, "ReloadDone")
                .GoTo("HipFire", "BackToHipFire")
            .Build();

        // ══════════════════════════════════════════════════════
        // Composite: wire it all together
        // ══════════════════════════════════════════════════════

        var player = new CompositeStateMachine("PlayerState");

        var locoTrace = new TraceBuffer(32);
        var postTrace = new TraceBuffer(16);
        var weapTrace = new TraceBuffer(32);

        var locoMachine = player.AddRegion("Locomotion", locomotionDef, locoTrace);
        var postMachine = player.AddRegion("Posture", postureDef, postTrace);
        var weapMachine = player.AddRegion("Weapon", weaponDef, weapTrace);

        // Cross-region constraint: running forces standing
        bool forcedStand = false;
        player.AddConstraint((composite, time) =>
        {
            var loco = composite.GetRegionState("Locomotion");
            var post = composite.GetRegionState("Posture");

            if (loco == "Running" && post == "Crouched" && !forcedStand)
            {
                Print("RULE", "Running forces standing posture!", ConsoleColor.Magenta);
                composite.SendEvent("Posture", "Stand", time);
                forcedStand = true;
            }
            else if (loco != "Running")
            {
                forcedStand = false;
            }
        });

        // Wire up transition logging
        locoMachine.OnTransition += t => LogTransition("LOCO", locomotionDef, t);
        postMachine.OnTransition += t => LogTransition("POST", postureDef, t);
        weapMachine.OnTransition += t => LogTransition("WEAP", weaponDef, t);

        // ══════════════════════════════════════════════════════
        // Simulate a gameplay sequence
        // ══════════════════════════════════════════════════════

        Print("ENGINE", "Starting composite state machine with 3 regions...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        player.Start(time);

        PrintSnapshot(player);

        // Sequence of player actions
        var actions = new (float delay, string description, Action<CompositeStateMachine, float> action)[]
        {
            (0.5f, "Player starts walking",
                (p, t) => p.SendEvent("Locomotion", "StartWalking", t)),

            (1.0f, "Player crouches",
                (p, t) => p.SendEvent("Posture", "Crouch", t)),

            (1.0f, "Player aims weapon",
                (p, t) => { weapMachine.Context.Set("weaponTarget", "aim"); p.SendEvent("Weapon", "WeaponCommand", t); }),

            (1.5f, "Player starts running (will force standing!)",
                (p, t) => { locoMachine.Context.Set("locoTarget", "run"); p.SendEvent("Locomotion", "LocoCommand", t); }),

            (1.0f, "Player reloads (stops aiming)",
                (p, t) => { weapMachine.Context.Set("weaponTarget", "reload"); p.SendEvent("Weapon", "WeaponCommand", t); }),

            (3.0f, "Reload finishes, player slows to walk",
                (p, t) => { locoMachine.Context.Set("locoTarget", "walk"); p.SendEvent("Locomotion", "LocoCommand", t); }),

            (1.0f, "Player crouches again",
                (p, t) => p.SendEvent("Posture", "Crouch", t)),

            (1.0f, "Player aims again",
                (p, t) => { weapMachine.Context.Set("weaponTarget", "aim"); p.SendEvent("Weapon", "WeaponCommand", t); }),

            (1.0f, "Player stops moving",
                (p, t) => { locoMachine.Context.Set("locoTarget", "stop"); p.SendEvent("Locomotion", "LocoCommand", t); }),
        };

        foreach (var (delay, description, action) in actions)
        {
            // Advance time and tick
            float target = time + delay;
            while (time < target)
            {
                time += 0.1f;
                player.Update(time);
            }

            Console.WriteLine();
            Print("INPUT", description, ConsoleColor.DarkGray);
            action(player, time);
            player.Update(time);
            PrintSnapshot(player);
        }

        // ── Timelines ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));

        PrintTimeline("LOCOMOTION", locomotionDef, locoTrace);
        PrintTimeline("POSTURE", postureDef, postTrace);
        PrintTimeline("WEAPON", weaponDef, weapTrace);

        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
        Print("RESULT", $"3 regions, {player.RegionCount} machines, zero combinatorial explosion.");
        Print("RESULT", "Each region is independent. Cross-region rules enforce constraints.");
        Print("RESULT", "Monolithic approach would need 18+ states. This needs 8.");
    }

    static void PrintSnapshot(CompositeStateMachine composite)
    {
        var snap = composite.GetSnapshot();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  [STATE      ] { ");
        bool first = true;
        foreach (var region in snap.Regions)
        {
            if (!first) Console.Write(", ");
            Console.Write($"{region.RegionName}: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(region.StateName);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            first = false;
        }
        Console.WriteLine(" }");
        Console.ResetColor();
    }

    static void Print(string tag, string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{tag,-12}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    static void LogTransition(string prefix, MachineDefinition def, TransitionTrace t)
    {
        var from = def.NameLookup.GetStateName(t.FromState);
        var to = def.NameLookup.GetStateName(t.ToState);
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ── [{prefix}] {from} → {to} | {t.Reason}: {t.Detail}");
        Console.ResetColor();
    }

    static void PrintTimeline(string label, MachineDefinition def, TraceBuffer buffer)
    {
        Print("TIMELINE", $"{label}:", ConsoleColor.Yellow);
        var traces = buffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = def.NameLookup.GetStateName(t.FromState);
            var to = def.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"    [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail})");
        }
        Console.WriteLine();
    }
}
