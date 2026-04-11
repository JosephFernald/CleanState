// =============================================================================
// CleanState Sample: Parallel / Sidecar Behavior
// =============================================================================
//
// This sample demonstrates the Scheduler's ability to run multiple machines
// concurrently — a main flow alongside independent sidecar machines.
//
// ── The scenario ──
//
//   A game round runs as the main flow:
//     Countdown → Play → ScoreScreen
//
//   Meanwhile, two sidecar machines run in parallel:
//     - Hint System: periodically shows hints during the Play phase
//     - Timeout Watchdog: enforces a hard time limit on the Play phase
//
//   All three machines share the same scheduler. The main flow sends events
//   that the sidecars react to, and the watchdog can force-end the round.
//
// ── The typical approach ──
//
//   // Scattered across multiple MonoBehaviours:
//   float hintTimer;
//   float watchdogTimer;
//   bool isPlaying;
//   bool hintShown;
//   void Update() {
//       if (isPlaying && Time.time > hintTimer) { ShowHint(); ... }
//       if (isPlaying && Time.time > watchdogTimer) { ForceEnd(); ... }
//   }
//
//   Coupled, fragile, untraceable.
//
// ── The CleanState approach ──
//
//   Each concern is its own machine with its own lifecycle.
//   The scheduler drives them all. No coupling. No shared booleans.
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.ParallelSidecar;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Parallel / Sidecar Behavior   ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Three machines running concurrently:               ║");
        Console.WriteLine("║  - Main: Countdown → Play → Score                  ║");
        Console.WriteLine("║  - Hint sidecar: shows hints during Play            ║");
        Console.WriteLine("║  - Watchdog sidecar: enforces time limit            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ══════════════════════════════════════════════════════
        // Machine 1: Main Game Round
        // ══════════════════════════════════════════════════════

        var mainDef = new MachineBuilder("GameRound")

            .State("Countdown")
                .TransitionIn(ctx =>
                {
                    Print("MAIN", "3...", ConsoleColor.White);
                }, "ShowCountdown3")
                .WaitForTime(1.0f, "Count3")
                .Then(ctx => Print("MAIN", "2...", ConsoleColor.White), "ShowCountdown2")
                .WaitForTime(1.0f, "Count2")
                .Then(ctx => Print("MAIN", "1...", ConsoleColor.White), "ShowCountdown1")
                .WaitForTime(1.0f, "Count1")
                .Then(ctx =>
                {
                    Print("MAIN", "GO!", ConsoleColor.Green);
                    ctx.Set("score", 0);
                    ctx.Set("roundActive", true);
                }, "StartRound")
                .GoTo("Play", "GoToPlay")

            .State("Play")
                .TransitionIn(ctx =>
                {
                    Print("MAIN", "Round is LIVE — collecting points...", ConsoleColor.Green);
                }, "BeginPlay")
                .WaitForEvent("RoundEnd", "WaitForRoundEnd")
                .Then(ctx =>
                {
                    ctx.Set("roundActive", false);
                    Print("MAIN", "Round ended.", ConsoleColor.White);
                }, "EndPlay")
                .GoTo("ScoreScreen", "GoToScore")

            .State("ScoreScreen")
                .TransitionIn(ctx =>
                {
                    var score = ctx.Get<int>("score");
                    var reason = ctx.TryGet<string>("endReason", out var r) ? r : "unknown";
                    Console.WriteLine();
                    Print("MAIN", "╔════════════════════════╗", ConsoleColor.Cyan);
                    Print("MAIN", $"║  FINAL SCORE: {score,-8} ║", ConsoleColor.Cyan);
                    Print("MAIN", $"║  Ended by: {reason,-11} ║", ConsoleColor.Cyan);
                    Print("MAIN", "╚════════════════════════╝", ConsoleColor.Cyan);
                }, "ShowScore")
            .Build();

        // ══════════════════════════════════════════════════════
        // Machine 2: Hint System (sidecar)
        // ══════════════════════════════════════════════════════

        var hintDef = new MachineBuilder("HintSystem")

            .State("WaitForStart")
                .TransitionIn(ctx =>
                {
                    Print("HINT", "Hint system standing by...", ConsoleColor.DarkGray);
                }, "HintStandby")
                .WaitForEvent("PlayStarted", "WaitForPlayStart")
                .GoTo("ShowHint1", "GoToHint1")

            .State("ShowHint1")
                .TransitionIn(ctx =>
                {
                    Print("HINT", "Tip: Collect the golden items for bonus points!", ConsoleColor.Yellow);
                }, "DisplayHint1")
                .WaitForTime(2.5f, "HintDelay1")
                .GoTo("ShowHint2", "GoToHint2")

            .State("ShowHint2")
                .TransitionIn(ctx =>
                {
                    Print("HINT", "Tip: Watch for the multiplier power-up!", ConsoleColor.Yellow);
                }, "DisplayHint2")
                .WaitForTime(2.5f, "HintDelay2")
                .GoTo("ShowHint3", "GoToHint3")

            .State("ShowHint3")
                .TransitionIn(ctx =>
                {
                    Print("HINT", "Tip: Time is running out — focus on high-value targets!", ConsoleColor.Yellow);
                }, "DisplayHint3")
                .WaitForTime(2.0f, "HintDelay3")
                .GoTo("HintsDone", "GoToHintsDone")

            .State("HintsDone")
                .TransitionIn(ctx =>
                {
                    Print("HINT", "All hints delivered.", ConsoleColor.DarkGray);
                }, "HintsComplete")
            .Build();

        // ══════════════════════════════════════════════════════
        // Machine 3: Timeout Watchdog (sidecar)
        // ══════════════════════════════════════════════════════

        var watchdogDef = new MachineBuilder("TimeoutWatchdog")

            .State("WaitForStart")
                .TransitionIn(ctx =>
                {
                    Print("WATCH", "Watchdog armed.", ConsoleColor.DarkGray);
                }, "WatchdogStandby")
                .WaitForEvent("PlayStarted", "WaitForPlayStart")
                .Then(ctx =>
                {
                    ctx.Set("timeLimit", 8.0f);
                    Print("WATCH", "Timer started — 8.0s time limit.", ConsoleColor.DarkRed);
                }, "StartTimer")
                .GoTo("Ticking", "GoToTicking")

            .State("Ticking")
                .TransitionIn(ctx =>
                {
                    // Only initialize on first entry, not on re-entry from the loop
                    if (!ctx.Has("watchElapsed"))
                        ctx.Set("watchElapsed", 0.0f);
                }, "InitTicks")
                .WaitForTime(2.0f, "TickInterval")
                .Then(ctx =>
                {
                    var elapsed = ctx.Get<float>("watchElapsed") + 2.0f;
                    ctx.Set("watchElapsed", elapsed);
                    var limit = ctx.Get<float>("timeLimit");
                    var remaining = limit - elapsed;

                    if (remaining <= 2.0f)
                        Print("WATCH", $"WARNING: {remaining:F0}s remaining!", ConsoleColor.Red);
                    else
                        Print("WATCH", $"{remaining:F0}s remaining", ConsoleColor.DarkRed);
                }, "ReportTime")
                .Decision("TimeCheck")
                    .When(ctx => ctx.Get<float>("watchElapsed") >= ctx.Get<float>("timeLimit"), "Expired", "TimeLimitReached")
                    .Otherwise("Ticking", "StillTicking")

            .State("Expired")
                .TransitionIn(ctx =>
                {
                    Print("WATCH", "TIME'S UP! Forcing round end.", ConsoleColor.Red);
                }, "ForceEnd")
            .Build();

        // ══════════════════════════════════════════════════════
        // Set up the shared scheduler
        // ══════════════════════════════════════════════════════

        var mainTrace = new TraceBuffer(32);
        var hintTrace = new TraceBuffer(16);
        var watchTrace = new TraceBuffer(16);

        var scheduler = new Scheduler();
        var mainMachine = scheduler.CreateMachine(mainDef, mainTrace);
        var hintMachine = scheduler.CreateMachine(hintDef, hintTrace);
        var watchdogMachine = scheduler.CreateMachine(watchdogDef, watchTrace);

        // Wire up transition logging for all three
        mainMachine.OnTransition += t => LogTransition("MAIN", mainDef, t);
        hintMachine.OnTransition += t => LogTransition("HINT", hintDef, t);
        watchdogMachine.OnTransition += t => LogTransition("WATCH", watchdogDef, t);

        // Pre-resolve events
        var roundEndEvent = MachineBuilder.EventIdFrom(mainDef, "RoundEnd");
        var playStartedHint = MachineBuilder.EventIdFrom(hintDef, "PlayStarted");
        var playStartedWatch = MachineBuilder.EventIdFrom(watchdogDef, "PlayStarted");

        // ── Run all three machines ──

        Print("ENGINE", "Starting 3 machines on shared scheduler...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        mainMachine.Start(time);
        hintMachine.Start(time);
        watchdogMachine.Start(time);

        bool playStartedFired = false;
        float nextScoreTime = 0f;
        int scoreCount = 0;

        while (mainMachine.Status != MachineStatus.Completed)
        {
            time += 0.1f;
            scheduler.Update(time);

            // When main enters Play, notify the sidecars
            if (!playStartedFired && mainMachine.Context.TryGet<bool>("roundActive", out var active) && active)
            {
                playStartedFired = true;
                Print("EVENT", "Broadcasting PlayStarted to sidecars", ConsoleColor.DarkGray);
                hintMachine.SendEvent(playStartedHint, time);
                watchdogMachine.SendEvent(playStartedWatch, time);
                nextScoreTime = time + 1.5f;
            }

            // Simulate scoring during play
            if (playStartedFired && mainMachine.Status == MachineStatus.Blocked
                && mainMachine.BlockReason == BlockKind.WaitForEvent
                && time >= nextScoreTime && scoreCount < 4)
            {
                scoreCount++;
                var points = (scoreCount * 100) + 50;
                var current = mainMachine.Context.Get<int>("score") + points;
                mainMachine.Context.Set("score", current);
                Print("SCORE", $"+{points} points! (total: {current})", ConsoleColor.Green);
                nextScoreTime = time + 1.8f;
            }

            // When watchdog expires, end the round
            if (watchdogMachine.Status == MachineStatus.Completed && mainMachine.Status == MachineStatus.Blocked)
            {
                mainMachine.Context.Set("endReason", "watchdog");
                mainMachine.SendEvent(roundEndEvent, time);
            }
        }

        // ── Timeline for all machines ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));

        PrintTimeline("MAIN FLOW", mainDef, mainTrace);
        PrintTimeline("HINT SIDECAR", hintDef, hintTrace);
        PrintTimeline("WATCHDOG", watchdogDef, watchTrace);

        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
        Print("RESULT", $"Scheduler drove {scheduler.MachineCount} machines concurrently.");
        Print("RESULT", "Each machine had its own lifecycle, trace buffer, and state.");
        Print("RESULT", "No shared booleans. No coupled Update loops. Full isolation.");
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
        Console.WriteLine($"  ── [{prefix}] {from} → {to} | {t.Reason}: {t.Detail} | t={t.Timestamp:F1}s");
        Console.ResetColor();
    }

    static void PrintTimeline(string label, MachineDefinition def, TraceBuffer buffer)
    {
        Print("TIMELINE", $"{label}:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));
        var traces = buffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = def.NameLookup.GetStateName(t.FromState);
            var to = def.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1,2}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail}) at t={t.Timestamp:F1}s");
        }
        Console.WriteLine();
    }
}
