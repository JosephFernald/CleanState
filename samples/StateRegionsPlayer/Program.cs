// =============================================================================
// CleanState Sample: State Regions Player
// =============================================================================
//
// A single machine is best for one concern.
// Some systems have multiple simultaneous concerns.
// This sample composes three machines to model one player without state explosion.
//
// ── Why not one machine? ──
//
//   A player can be Running, Crouched, and Aiming — all at the same time.
//   A single FSM must represent every combination as a discrete state:
//
//     IdleStandingHipFire         WalkingStandingHipFire
//     IdleStandingAiming          WalkingStandingAiming
//     IdleStandingReloading       WalkingStandingReloading
//     IdleCrouchedHipFire         WalkingCrouchedHipFire
//     IdleCrouchedAiming          WalkingCrouchedAiming
//     IdleCrouchedReloading       WalkingCrouchedReloading
//     IdleProneHipFire            WalkingProneHipFire
//     RunningStandingHipFire      SprintingStandingHipFire
//     RunningStandingAiming       SprintingStandingAiming (blocked!)
//     RunningCrouchedHipFire      SprintingCrouchedHipFire (blocked!)
//     RunningCrouchedAiming       ...
//     ...
//
//   4 locomotion x 3 posture x 3 weapon = 36 states minimum.
//   Add one more concern (e.g., NetworkAuthority) and it doubles again.
//
// ── The state regions approach ──
//
//   Three independent machines, each modeling one concern:
//     Locomotion: Idle, Walking, Running, Sprinting    (4 states)
//     Posture:    Standing, Crouched, Prone            (3 states)
//     Weapon:     HipFire, Aiming, Reloading           (3 states)
//
//   Total: 10 states. Not 36.
//
//   The aggregate player state is the tuple of all three:
//     { Locomotion: Running, Posture: Crouched, Weapon: Aiming }
//
//   Cross-region rules enforce constraints:
//     - Sprinting cancels aiming → weapon forced to HipFire
//     - Reloading blocks aiming → weapon stays in Reloading until done
//     - Going prone cancels sprint → locomotion forced to Walking
//
// ── What this proves ──
//
//   1. State explosion is avoided (10 states vs 36+)
//   2. Debugging and tracing still work per-region
//   3. Cross-region constraints keep behavior coherent
//   4. Recovery snapshots work across all regions
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Composition;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.StateRegionsPlayer;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: State Regions Player          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  Why not one machine?                               ║");
        Console.WriteLine("║  4 locomotion x 3 posture x 3 weapon = 36 states    ║");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  State regions: 4 + 3 + 3 = 10 states              ║");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  Cross-region rules:                                ║");
        Console.WriteLine("║  - Sprinting cancels aiming                         ║");
        Console.WriteLine("║  - Reloading blocks aiming                          ║");
        Console.WriteLine("║  - Going prone cancels sprint                       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ══════════════════════════════════════════════════════
        // Region 1: Locomotion (4 states)
        // ══════════════════════════════════════════════════════

        var locomotionDef = new MachineBuilder("Locomotion")
            .State("Idle")
                .TransitionIn(ctx => {}, "EnterIdle")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("IdleDecision")
                    .When(ctx => LocoTarget(ctx) == "walk", "Walking", "IdleToWalk")
                    .When(ctx => LocoTarget(ctx) == "run", "Running", "IdleToRun")
                    .When(ctx => LocoTarget(ctx) == "sprint", "Sprinting", "IdleToSprint")
                    .Otherwise("Idle", "StayIdle")
            .State("Walking")
                .TransitionIn(ctx => {}, "EnterWalking")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("WalkDecision")
                    .When(ctx => LocoTarget(ctx) == "stop", "Idle", "WalkToIdle")
                    .When(ctx => LocoTarget(ctx) == "run", "Running", "WalkToRun")
                    .When(ctx => LocoTarget(ctx) == "sprint", "Sprinting", "WalkToSprint")
                    .Otherwise("Walking", "StayWalking")
            .State("Running")
                .TransitionIn(ctx => {}, "EnterRunning")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("RunDecision")
                    .When(ctx => LocoTarget(ctx) == "stop", "Idle", "RunToIdle")
                    .When(ctx => LocoTarget(ctx) == "walk", "Walking", "RunToWalk")
                    .When(ctx => LocoTarget(ctx) == "sprint", "Sprinting", "RunToSprint")
                    .Otherwise("Running", "StayRunning")
            .State("Sprinting")
                .TransitionIn(ctx => {}, "EnterSprinting")
                .WaitForEvent("LocoCommand", "WaitForLocoCmd")
                .Decision("SprintDecision")
                    .When(ctx => LocoTarget(ctx) == "stop", "Idle", "SprintToIdle")
                    .When(ctx => LocoTarget(ctx) == "walk", "Walking", "SprintToWalk")
                    .When(ctx => LocoTarget(ctx) == "run", "Running", "SprintToRun")
                    .Otherwise("Sprinting", "StaySprinting")
            .Build();

        // ══════════════════════════════════════════════════════
        // Region 2: Posture (3 states)
        // ══════════════════════════════════════════════════════

        var postureDef = new MachineBuilder("Posture")
            .State("Standing")
                .TransitionIn(ctx => {}, "EnterStanding")
                .WaitForEvent("PostureCommand", "WaitForPostureCmd")
                .Decision("StandDecision")
                    .When(ctx => PostureTarget(ctx) == "crouch", "Crouched", "StandToCrouch")
                    .When(ctx => PostureTarget(ctx) == "prone", "Prone", "StandToProne")
                    .Otherwise("Standing", "StayStanding")
            .State("Crouched")
                .TransitionIn(ctx => {}, "EnterCrouched")
                .WaitForEvent("PostureCommand", "WaitForPostureCmd")
                .Decision("CrouchDecision")
                    .When(ctx => PostureTarget(ctx) == "stand", "Standing", "CrouchToStand")
                    .When(ctx => PostureTarget(ctx) == "prone", "Prone", "CrouchToProne")
                    .Otherwise("Crouched", "StayCrouched")
            .State("Prone")
                .TransitionIn(ctx => {}, "EnterProne")
                .WaitForEvent("PostureCommand", "WaitForPostureCmd")
                .Decision("ProneDecision")
                    .When(ctx => PostureTarget(ctx) == "stand", "Standing", "ProneToStand")
                    .When(ctx => PostureTarget(ctx) == "crouch", "Crouched", "ProneToCrouch")
                    .Otherwise("Prone", "StayProne")
            .Build();

        // ══════════════════════════════════════════════════════
        // Region 3: Weapon (3 states)
        // ══════════════════════════════════════════════════════

        var weaponDef = new MachineBuilder("Weapon")
            .State("HipFire")
                .TransitionIn(ctx => {}, "EnterHipFire")
                .WaitForEvent("WeaponCommand", "WaitForWeaponCmd")
                .Decision("HipDecision")
                    .When(ctx => WeaponTarget(ctx) == "aim", "Aiming", "HipToAim")
                    .When(ctx => WeaponTarget(ctx) == "reload", "Reloading", "HipToReload")
                    .Otherwise("HipFire", "StayHipFire")
            .State("Aiming")
                .TransitionIn(ctx => {}, "EnterAiming")
                .WaitForEvent("WeaponCommand", "WaitForWeaponCmd")
                .Decision("AimDecision")
                    .When(ctx => WeaponTarget(ctx) == "hipfire", "HipFire", "AimToHip")
                    .When(ctx => WeaponTarget(ctx) == "reload", "Reloading", "AimToReload")
                    .Otherwise("Aiming", "StayAiming")
            .State("Reloading")
                .TransitionIn(ctx => {}, "EnterReloading")
                .WaitForTime(2.0f, "ReloadDuration")
                .GoTo("HipFire", "ReloadComplete")
            .Build();

        // ══════════════════════════════════════════════════════
        // Wire up the composite
        // ══════════════════════════════════════════════════════

        var player = new CompositeStateMachine("PlayerState");

        var locoTrace = new TraceBuffer(32);
        var postTrace = new TraceBuffer(32);
        var weapTrace = new TraceBuffer(32);

        var locoMachine = player.AddRegion("Locomotion", locomotionDef, locoTrace);
        var postMachine = player.AddRegion("Posture", postureDef, postTrace);
        var weapMachine = player.AddRegion("Weapon", weaponDef, weapTrace);

        // ── Cross-region rules ──

        // Rule 1: Sprinting cancels aiming
        player.AddConstraint((c, t) =>
        {
            if (c.GetRegionState("Locomotion") == "Sprinting"
                && c.GetRegionState("Weapon") == "Aiming")
            {
                Print("RULE", "Sprinting cancels aiming → weapon forced to HipFire", ConsoleColor.Magenta);
                weapMachine.Context.Set("weaponTarget", "hipfire");
                c.SendEvent("Weapon", "WeaponCommand", t);
            }
        });

        // Rule 2: Going prone cancels sprint
        player.AddConstraint((c, t) =>
        {
            if (c.GetRegionState("Posture") == "Prone"
                && c.GetRegionState("Locomotion") == "Sprinting")
            {
                Print("RULE", "Prone cancels sprint → locomotion forced to Walking", ConsoleColor.Magenta);
                locoMachine.Context.Set("locoTarget", "walk");
                c.SendEvent("Locomotion", "LocoCommand", t);
            }
        });

        // Wire up transition logging
        locoMachine.OnTransition += t => LogTransition("LOCO", locomotionDef, t);
        postMachine.OnTransition += t => LogTransition("POST", postureDef, t);
        weapMachine.OnTransition += t => LogTransition("WEAP", weaponDef, t);

        // ══════════════════════════════════════════════════════
        // Simulate a gameplay sequence
        // ══════════════════════════════════════════════════════

        Print("ENGINE", "Starting player with 3 state regions (10 states total)...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        player.Start(time);
        PrintState(player, "Initial");

        // The input script — each action tells a story
        var script = new (float delay, string narration, Action<float> action)[]
        {
            // Player starts moving
            (0.5f, "Player starts walking forward",
                t => SendLoco(player, locoMachine, "walk", t)),

            // Player crouches while walking
            (0.8f, "Player crouches",
                t => SendPosture(player, postMachine, "crouch", t)),

            // Player aims down sights while crouched
            (0.8f, "Player aims down sights",
                t => SendWeapon(player, weapMachine, "aim", t)),

            // Player starts sprinting — this should CANCEL aiming
            (1.0f, "Player starts sprinting (should cancel aiming!)",
                t => SendLoco(player, locoMachine, "sprint", t)),

            // Player stops sprinting, returns to running
            (1.0f, "Player releases sprint, back to running",
                t => SendLoco(player, locoMachine, "run", t)),

            // Player aims again (now allowed)
            (0.5f, "Player aims again (allowed now)",
                t => SendWeapon(player, weapMachine, "aim", t)),

            // Player goes prone — this should CANCEL sprint if active
            (1.0f, "Player goes prone",
                t => SendPosture(player, postMachine, "prone", t)),

            // Player reloads while prone
            (0.8f, "Player reloads while prone",
                t => SendWeapon(player, weapMachine, "reload", t)),

            // Try to aim during reload — weapon is locked in Reloading
            (0.5f, "Player tries to aim (blocked — still reloading!)",
                t =>
                {
                    if (player.GetRegionState("Weapon") == "Reloading")
                        Print("BLOCK", "Cannot aim while reloading — weapon is locked", ConsoleColor.Red);
                }),

            // Wait for reload to finish
            (2.5f, "Reload finishes automatically",
                t => {} ),

            // Player stands up and stops
            (0.5f, "Player stands up",
                t => SendPosture(player, postMachine, "stand", t)),

            (0.5f, "Player stops moving",
                t => SendLoco(player, locoMachine, "stop", t)),
        };

        foreach (var (delay, narration, action) in script)
        {
            // Advance time
            float target = time + delay;
            while (time < target)
            {
                time += 0.05f;
                player.Update(time);
            }

            Console.WriteLine();
            Print("INPUT", narration, ConsoleColor.DarkGray);
            action(time);
            player.Update(time);
            PrintState(player, null);
        }

        // ── State explosion comparison ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("COMPARE", "State count comparison:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));
        Print("COMPARE", "Monolithic FSM:  4 x 3 x 3 = 36 states (minimum)", ConsoleColor.Red);
        Print("COMPARE", "State Regions:   4 + 3 + 3 = 10 states", ConsoleColor.Green);
        Print("COMPARE", "Add NetworkAuth (4 states):", ConsoleColor.Yellow);
        Print("COMPARE", "  Monolithic:    36 x 4 = 144 states", ConsoleColor.Red);
        Print("COMPARE", "  State Regions: 10 + 4 = 14 states", ConsoleColor.Green);
        Console.WriteLine(new string('═', 60));

        // ── Per-region timelines ──

        Console.WriteLine();
        PrintTimeline("LOCOMOTION", locomotionDef, locoTrace);
        PrintTimeline("POSTURE", postureDef, postTrace);
        PrintTimeline("WEAPON", weaponDef, weapTrace);

        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
        Print("RESULT", "Each region has its own trace — debuggable independently.");
        Print("RESULT", "Cross-region rules enforced constraints without coupling.");
        Print("RESULT", "Recovery snapshots would capture all 3 regions independently.");
    }

    // ── Command helpers ──

    static void SendLoco(CompositeStateMachine player, Machine machine, string target, float time)
    {
        machine.Context.Set("locoTarget", target);
        player.SendEvent("Locomotion", "LocoCommand", time);
    }

    static void SendPosture(CompositeStateMachine player, Machine machine, string target, float time)
    {
        machine.Context.Set("postureTarget", target);
        player.SendEvent("Posture", "PostureCommand", time);
    }

    static void SendWeapon(CompositeStateMachine player, Machine machine, string target, float time)
    {
        machine.Context.Set("weaponTarget", target);
        player.SendEvent("Weapon", "WeaponCommand", time);
    }

    static string LocoTarget(MachineContext ctx) =>
        ctx.TryGet<string>("locoTarget", out var t) ? t : "";

    static string PostureTarget(MachineContext ctx) =>
        ctx.TryGet<string>("postureTarget", out var t) ? t : "";

    static string WeaponTarget(MachineContext ctx) =>
        ctx.TryGet<string>("weaponTarget", out var t) ? t : "";

    // ── Display helpers ──

    static void PrintState(CompositeStateMachine player, string label)
    {
        var snap = player.GetSnapshot();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(label != null ? $"  [{label,-12}] {{ " : "  [STATE       ] { ");
        bool first = true;
        foreach (var region in snap.Regions)
        {
            if (!first) Console.Write(", ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"{region.RegionName}: ");
            Console.ForegroundColor = region.Status == MachineStatus.Blocked
                ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write(region.StateName);
            first = false;
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
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
