// =============================================================================
// CleanState Sample: Mission Control — WaitForAll / WaitForAny
// =============================================================================
//
// This sample demonstrates the composite wait blocks: WaitForAll and WaitForAny.
//
// ── The scenario ──
//
//   A rocket launch sequence with three phases:
//
//   1. PREFLIGHT — All subsystems must report ready before launch can proceed.
//      Uses WaitForAll to gate on Navigation + Propulsion events, a Telemetry
//      predicate, and a minimum 2-second hold timer — all four simultaneously.
//
//   2. LAUNCH — The engines ignite and we race against a possible abort.
//      Uses WaitForAny to wait for either a "LaunchSuccess" confirmation,
//      an "Abort" signal, or a 15-second safety timeout — whichever comes
//      first determines the next state.
//
//   3. OUTCOME — Orbit (success), scrubbed (abort), or anomaly (timeout).
//
// The sample runs all three scenarios to show each WaitForAny outcome.
//
// ── What this proves ──
//
//   - WaitForAll: gate on multiple independent conditions (events + predicate + time)
//   - WaitForAny: race between competing outcomes (event vs. event vs. time)
//   - Composite waits integrate naturally with the fluent builder API
//   - Events, time, and predicates can be freely mixed in a single wait
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.MissionControl;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Mission Control               ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Demonstrates WaitForAll and WaitForAny:            ║");
        Console.WriteLine("║  - WaitForAll: all subsystems must report ready     ║");
        Console.WriteLine("║  - WaitForAny: race launch success vs. abort        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");

        // Shared flag — telemetry link status checked by WaitForAll predicate
        var telemetry = new Flag();

        var definition = BuildLaunchSequence(telemetry);

        // ── Scenario 1: Successful launch ──
        Console.WriteLine();
        Console.WriteLine(new string('━', 60));
        Print("SCENARIO", "1 — Successful Launch", ConsoleColor.Cyan);
        Console.WriteLine(new string('━', 60));
        RunScenario(definition, telemetry, "success");

        // ── Scenario 2: Abort during launch ──
        Console.WriteLine();
        Console.WriteLine(new string('━', 60));
        Print("SCENARIO", "2 — Launch Abort", ConsoleColor.Cyan);
        Console.WriteLine(new string('━', 60));
        RunScenario(definition, telemetry, "abort");

        // ── Scenario 3: Safety timeout ──
        Console.WriteLine();
        Console.WriteLine(new string('━', 60));
        Print("SCENARIO", "3 — Safety Timeout", ConsoleColor.Cyan);
        Console.WriteLine(new string('━', 60));
        RunScenario(definition, telemetry, "timeout");
    }

    static MachineDefinition BuildLaunchSequence(Flag telemetry)
    {
        return new MachineBuilder("LaunchSequence")

            // ── Phase 1: Preflight (WaitForAll) ─────────────────────
            //
            // ALL of these must be satisfied before we proceed:
            //   - "NavReady" event received
            //   - "PropulsionReady" event received
            //   - Telemetry link predicate returns true
            //   - 2-second minimum hold timer has elapsed

            .State("Preflight")
                .TransitionIn(ctx =>
                {
                    ctx.Set("missionId", "CLEANSTATE-1");
                    Print("PREFLIGHT", "Launch sequence initiated.");
                    Print("PREFLIGHT", $"  Mission: {ctx.Get<string>("missionId")}");
                    Print("PREFLIGHT", "Waiting for all subsystems...");
                    Print("PREFLIGHT", "  [ ] Navigation     (event)");
                    Print("PREFLIGHT", "  [ ] Propulsion     (event)");
                    Print("PREFLIGHT", "  [ ] Telemetry      (predicate)");
                    Print("PREFLIGHT", "  [ ] Hold timer 2s  (time)");
                }, "InitMission")

                .WaitForAll(w => w
                    .Event("NavReady")
                    .Event("PropulsionReady")
                    .Predicate(ctx => telemetry.Value)
                    .Time(2.0),
                    "AwaitAllSystems")

                .Then(ctx =>
                {
                    Print("PREFLIGHT", "  [x] Navigation     GO", ConsoleColor.Green);
                    Print("PREFLIGHT", "  [x] Propulsion     GO", ConsoleColor.Green);
                    Print("PREFLIGHT", "  [x] Telemetry      GO", ConsoleColor.Green);
                    Print("PREFLIGHT", "  [x] Hold timer     GO", ConsoleColor.Green);
                    Print("PREFLIGHT", "ALL SYSTEMS GO.", ConsoleColor.Green);
                }, "AllSystemsGo")
                .GoTo("Launch", "ProceedToLaunch")

            // ── Phase 2: Launch (WaitForAny) ────────────────────────
            //
            // FIRST of these to fire determines the outcome:
            //   - "LaunchSuccess" event  → orbit
            //   - "Abort" event          → scrubbed
            //   - 15-second timeout      → safety anomaly

            .State("Launch")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("LAUNCH", "MAIN ENGINE IGNITION", ConsoleColor.Yellow);
                    Print("LAUNCH", "Racing: Success vs. Abort vs. 15s timeout...");
                }, "EngineIgnition")

                .WaitForAny(w => w
                    .Event("LaunchSuccess")
                    .Event("Abort")
                    .Time(15.0),
                    "RaceLaunchOutcome")

                // Route based on which outcome the caller set in context
                .Decision("RouteOutcome")
                    .When(ctx => ctx.Get<string>("outcome") == "success", "Orbit", "SuccessPath")
                    .When(ctx => ctx.Get<string>("outcome") == "abort", "Scrubbed", "AbortPath")
                    .Otherwise("Anomaly", "TimeoutPath")

            // ── Outcomes ────────────────────────────────────────────

            .State("Orbit")
                .TransitionIn(ctx =>
                {
                    var id = ctx.Get<string>("missionId");
                    Console.WriteLine();
                    Print("ORBIT", "========================================", ConsoleColor.Green);
                    Print("ORBIT", $"  MISSION SUCCESS — {id} in orbit!", ConsoleColor.Green);
                    Print("ORBIT", "========================================", ConsoleColor.Green);
                }, "AnnounceOrbit")

            .State("Scrubbed")
                .TransitionIn(ctx =>
                {
                    var id = ctx.Get<string>("missionId");
                    Console.WriteLine();
                    Print("SCRUB", "========================================", ConsoleColor.Red);
                    Print("SCRUB", $"  LAUNCH SCRUBBED — {id} aborted.", ConsoleColor.Red);
                    Print("SCRUB", "========================================", ConsoleColor.Red);
                }, "AnnounceScrub")

            .State("Anomaly")
                .TransitionIn(ctx =>
                {
                    var id = ctx.Get<string>("missionId");
                    Console.WriteLine();
                    Print("ANOMALY", "========================================", ConsoleColor.DarkYellow);
                    Print("ANOMALY", $"  SAFETY TIMEOUT — {id}", ConsoleColor.DarkYellow);
                    Print("ANOMALY", "  No confirmation. Entering safe mode.", ConsoleColor.DarkYellow);
                    Print("ANOMALY", "========================================", ConsoleColor.DarkYellow);
                }, "AnnounceAnomaly")

            .Build();
    }

    static void RunScenario(MachineDefinition definition, Flag telemetry, string scenario)
    {
        Console.WriteLine();
        telemetry.Value = false;

        var traceBuffer = new TraceBuffer(32);
        var scheduler = new Scheduler();
        var machine = scheduler.CreateMachine(definition, traceBuffer);

        machine.OnTransition += trace =>
        {
            var from = definition.NameLookup.GetStateName(trace.FromState);
            var to = definition.NameLookup.GetStateName(trace.ToState);
            Print("TRANS", $"{from} -> {to}  ({trace.Detail})", ConsoleColor.DarkGray);
        };

        var navReady = MachineBuilder.EventIdFrom(definition, "NavReady");
        var propReady = MachineBuilder.EventIdFrom(definition, "PropulsionReady");
        var success = MachineBuilder.EventIdFrom(definition, "LaunchSuccess");
        var abort = MachineBuilder.EventIdFrom(definition, "Abort");

        // ── Preflight: subsystems check in one by one ──

        double t = 0.0;
        machine.Start(t);
        Print("SIM", $"[t={t:F1}s] Machine started — blocked on WaitForAll");

        t = 0.5;
        Print("SIM", $"[t={t:F1}s] Navigation reports ready.", ConsoleColor.DarkCyan);
        machine.SendEvent(navReady, t);

        t = 1.0;
        Print("SIM", $"[t={t:F1}s] Propulsion reports ready.", ConsoleColor.DarkCyan);
        machine.SendEvent(propReady, t);

        t = 1.5;
        Print("SIM", $"[t={t:F1}s] Telemetry link established.", ConsoleColor.DarkCyan);
        telemetry.Value = true;
        scheduler.Update(t);

        // Still blocked — hold timer needs 2.0s from start
        Print("SIM", $"        Status: {machine.Status} (hold timer counting...)", ConsoleColor.DarkGray);

        t = 2.5;
        Print("SIM", $"[t={t:F1}s] Hold timer complete.", ConsoleColor.DarkCyan);
        scheduler.Update(t);
        Print("SIM", $"        Status: {machine.Status} — now in Launch phase", ConsoleColor.DarkGray);

        // ── Launch: outcome depends on scenario ──

        switch (scenario)
        {
            case "success":
                t = 5.0;
                Print("SIM", $"[t={t:F1}s] Flight confirms nominal trajectory.", ConsoleColor.DarkCyan);
                machine.Context.Set("outcome", "success");
                machine.SendEvent(success, t);
                break;

            case "abort":
                t = 4.0;
                Print("SIM", $"[t={t:F1}s] Range safety triggers ABORT.", ConsoleColor.DarkCyan);
                machine.Context.Set("outcome", "abort");
                machine.SendEvent(abort, t);
                break;

            case "timeout":
                Print("SIM", "        No signals received...", ConsoleColor.DarkGray);
                machine.Context.Set("outcome", "timeout");
                t = 18.0;
                Print("SIM", $"[t={t:F1}s] Safety timeout reached.", ConsoleColor.DarkCyan);
                scheduler.Update(t);
                break;
        }

        // ── Print transition timeline ──

        Console.WriteLine();
        Print("TIMELINE", "Transitions:", ConsoleColor.Yellow);

        var traces = traceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var tr = traces[i];
            var from = definition.NameLookup.GetStateName(tr.FromState);
            var to = definition.NameLookup.GetStateName(tr.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"    [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} -> {to}  (t={tr.Timestamp:F1}s, {tr.Detail})");
        }

        Print("RESULT", $"Final: {machine.Status}", ConsoleColor.White);
    }

    static void Print(string tag, string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{tag,-12}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>Mutable wrapper so closures can share a bool with the caller.</summary>
    class Flag { public bool Value; }
}
