// =============================================================================
// CleanState Sample: Slot-Style Pick Game
// =============================================================================
//
// This sample demonstrates how CleanState handles complex gameplay orchestration:
//
//   - Branching logic and decision trees
//   - Repeated loops (pick → reveal → check → repeat)
//   - Recovery checkpoints
//   - Full transition traceability
//   - Debug breakpoints and timeline replay
//
// ── The typical approach ──
//
//   bool isRevealing;
//   bool hasMorePicks;
//   int picksRemaining;
//   IEnumerator RevealCoroutine() { ... }
//
//   Scattered state, hidden flow, impossible to trace or recover.
//
// ── The CleanState approach ──
//
//   Every step is named. Every transition has a reason.
//   You can pause mid-reveal, inspect the timeline, and recover from any checkpoint.
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.PickGame;

class Program
{
    static readonly Random Rng = new(42); // Fixed seed for reproducibility

    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          CleanState Sample: Pick Game               ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Player picks items from a grid.                    ║");
        Console.WriteLine("║  Each pick reveals a prize or a GAME OVER.          ║");
        Console.WriteLine("║  Picks loop until the player runs out or hits end.  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build the machine ──

        var definition = new MachineBuilder("PickGame")

            .State("Setup")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    // Initialize game state
                    ctx.Set("totalPrize", 0);
                    ctx.Set("picksRemaining", 5);
                    ctx.Set("picksMade", 0);
                    ctx.Set("gameOver", false);

                    // Build the hidden grid: prizes and one GAME OVER
                    var grid = new[] { 100, 250, 500, 50, 0, 150, 75, 200, -1 }; // -1 = game over
                    ctx.Set("grid", grid);
                    ctx.Set("revealed", new bool[grid.Length]);

                    Print("SETUP", "Game initialized. 5 picks available. Find prizes — avoid the GAME OVER!");
                    PrintGrid(ctx);
                }, "InitializeGame")
                .GoTo("AwaitPick", "GoToAwaitPick")

            .State("AwaitPick")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var remaining = ctx.Get<int>("picksRemaining");
                    var total = ctx.Get<int>("totalPrize");
                    Print("AWAIT", $"Picks remaining: {remaining} | Total prize: ${total}");
                    Print("AWAIT", "Waiting for player pick...");
                }, "ShowPickPrompt")
                .WaitForEvent("PlayerPicked", "WaitForPick")
                .Then(ctx =>
                {
                    // Simulate player choosing a random unrevealed cell
                    var grid = ctx.Get<int[]>("grid");
                    var revealed = ctx.Get<bool[]>("revealed");

                    int choice;
                    do { choice = Rng.Next(grid.Length); }
                    while (revealed[choice]);

                    revealed[choice] = true;
                    ctx.Set("lastChoice", choice);
                    ctx.Set("lastValue", grid[choice]);
                    ctx.Set("picksMade", ctx.Get<int>("picksMade") + 1);
                    ctx.Set("picksRemaining", ctx.Get<int>("picksRemaining") - 1);

                    Print("PICK", $"Player picked cell [{choice}]");
                }, "ProcessPick")
                .GoTo("Reveal", "GoToReveal")

            .State("Reveal")
                .TransitionIn(ctx =>
                {
                    var choice = ctx.Get<int>("lastChoice");
                    var value = ctx.Get<int>("lastValue");

                    if (value == -1)
                    {
                        Print("REVEAL", $"Cell [{choice}] → GAME OVER!", ConsoleColor.Red);
                        ctx.Set("gameOver", true);
                    }
                    else
                    {
                        Print("REVEAL", $"Cell [{choice}] → Prize: ${value}!", ConsoleColor.Green);
                        ctx.Set("totalPrize", ctx.Get<int>("totalPrize") + value);
                    }

                    PrintGrid(ctx);
                }, "RevealResult")
                .WaitForTime(0.5f, "RevealPause")
                .Then(ctx =>
                {
                    var total = ctx.Get<int>("totalPrize");
                    Print("REVEAL", $"Running total: ${total}");
                }, "ShowRunningTotal")
                .Decision("RevealDecision")
                    .When(ctx => ctx.Get<bool>("gameOver"), "GameOver", "HitGameOver")
                    .When(ctx => ctx.Get<int>("picksRemaining") <= 0, "Summary", "NoPicks")
                    .Otherwise("AwaitPick", "ContinuePicking")

            .State("GameOver")
                .TransitionIn(ctx =>
                {
                    var total = ctx.Get<int>("totalPrize");
                    var picks = ctx.Get<int>("picksMade");
                    Console.WriteLine();
                    Print("GAME OVER", $"Bad luck! Hit GAME OVER after {picks} pick(s).", ConsoleColor.Red);
                    Print("GAME OVER", $"Final prize: ${total}", ConsoleColor.Red);
                }, "ShowGameOver")
                .GoTo("Done", "GoToDone")

            .State("Summary")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var total = ctx.Get<int>("totalPrize");
                    var picks = ctx.Get<int>("picksMade");
                    Console.WriteLine();
                    Print("SUMMARY", $"All picks used! Made {picks} picks.", ConsoleColor.Cyan);
                    Print("SUMMARY", $"Total prize: ${total}", ConsoleColor.Cyan);
                }, "ShowSummary")
                .WaitForTime(1.0f, "SummaryPause")
                .GoTo("Done", "GoToDone")

            .State("Done")
                .TransitionIn(ctx =>
                {
                    Print("DONE", "Game complete.");
                }, "FinalMessage")
            .Build();

        // ── Set up scheduler and tracing ──

        var traceBuffer = new TraceBuffer(64);
        var scheduler = new Scheduler();
        var machine = scheduler.CreateMachine(definition, traceBuffer);

        // Subscribe to transitions for live logging
        machine.OnTransition += trace =>
        {
            var fromName = definition.NameLookup.GetStateName(trace.FromState);
            var toName = definition.NameLookup.GetStateName(trace.ToState);
            PrintTrace(fromName, toName, trace.Reason, trace.Detail, trace.Timestamp);
        };

        // ── Set up debug controller with breakpoint on Reveal ──

        var debugCtrl = new FsmDebugController(machine);

        // Find the Reveal state ID and set a breakpoint
        for (int i = 0; i < definition.StateCount; i++)
        {
            var state = definition.GetStateByIndex(i);
            if (definition.NameLookup.GetStateName(state.Id) == "Reveal")
            {
                debugCtrl.AddBreakpoint(FsmBreakpoint.OnStateEntry(state.Id));
                Print("DEBUG", "Breakpoint set on state: Reveal", ConsoleColor.Magenta);
                break;
            }
        }

        // ── Run the game ──

        Console.WriteLine();
        Print("ENGINE", "Starting machine...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        machine.Start(time);

        var pickEvent = MachineBuilder.EventIdFrom(definition, "PlayerPicked");

        while (machine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            // Handle debug controller — breakpoint pauses the machine.
            // When paused by a breakpoint, the machine is Blocked with BlockKind.None.
            // The scheduler can't resume BlockKind.None, so we must kick the machine
            // manually after Resume().
            if (debugCtrl.IsPaused)
            {
                if (debugCtrl.BreakpointHit)
                {
                    var bp = debugCtrl.LastHitBreakpoint;
                    var snap = machine.GetDebugSnapshot();
                    Console.WriteLine();
                    Print("BREAKPOINT", $"Hit breakpoint: {bp.Kind} on state '{snap.CurrentStateName}'", ConsoleColor.Magenta);
                    Print("BREAKPOINT", $"  Status: {snap.Status} | Step: {snap.CurrentStepIndex} ({snap.CurrentStepLabel})", ConsoleColor.Magenta);

                    // Disable the breakpoint after first hit so we can see the rest of the game
                    bp.Enabled = false;
                    Print("BREAKPOINT", "  Breakpoint disabled. Resuming...", ConsoleColor.Magenta);
                    Console.WriteLine();
                }
                debugCtrl.Resume();
                // Manually re-enter execution since BlockKind.None is not resumable by the scheduler
                machine.ForceState(machine.CurrentState, time, machine.CurrentStepIndex);
                continue;
            }

            // When blocked waiting for an event, simulate the player picking
            if (machine.Status == MachineStatus.Blocked && machine.BlockReason == BlockKind.WaitForEvent)
            {
                machine.SendEvent(pickEvent, time);
            }

            scheduler.Update(time);
        }

        // ── Show transition timeline ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("TIMELINE", "Full transition history:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));

        var traces = traceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = definition.NameLookup.GetStateName(t.FromState);
            var to = definition.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail}) at t={t.Timestamp:F1}s");
        }

        Console.WriteLine(new string('═', 60));

        // ── Final debug snapshot ──

        var finalSnap = machine.GetDebugSnapshot();
        Console.WriteLine();
        Print("SNAPSHOT", $"Machine: {finalSnap.MachineName}");
        Print("SNAPSHOT", $"Status:  {finalSnap.Status}");
        Print("SNAPSHOT", $"State:   {finalSnap.CurrentStateName}");
        Print("SNAPSHOT", $"Transitions recorded: {traceBuffer.Count}");
    }

    // ── Helpers ──

    static void Print(string tag, string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{tag,-12}] ");
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    static void PrintTrace(string from, string to, TransitionReasonKind reason, string detail, float time)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  ── {from} → {to} | {reason}: {detail} | t={time:F1}s");
        Console.ResetColor();
    }

    static void PrintGrid(MachineContext ctx)
    {
        var grid = ctx.Get<int[]>("grid");
        var revealed = ctx.Get<bool[]>("revealed");

        Console.Write("  [GRID       ] ");
        for (int i = 0; i < grid.Length; i++)
        {
            if (revealed[i])
            {
                if (grid[i] == -1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(" XX ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"${grid[i],-3}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" ?? ");
            }

            if ((i + 1) % 3 == 0 && i < grid.Length - 1)
            {
                Console.ResetColor();
                Console.Write("│");
            }
        }
        Console.ResetColor();
        Console.WriteLine();
    }
}
