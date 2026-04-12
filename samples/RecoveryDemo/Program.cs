// =============================================================================
// CleanState Sample: Save/Load + Recovery
// =============================================================================
//
// This sample demonstrates CleanState's biggest architectural differentiator:
//
//   Machines can be interrupted at any point, serialized, and restored
//   to continue exactly where they left off.
//
// ── The problem with typical FSMs ──
//
//   Most FSM systems assume execution never stops. But real systems do:
//   - Power loss mid-flow
//   - App backgrounded / killed
//   - Network interruption during async work
//   - Server restart during a multi-step process
//
//   Result: broken state, corrupted flows, "restart from beginning" hacks.
//
// ── The CleanState approach ──
//
//   1. Mark stable points as checkpoints
//   2. Capture a snapshot (state + domain data)
//   3. Kill the machine
//   4. Create a new machine from the same definition
//   5. Restore from snapshot
//   6. Execution resumes correctly
//
// ── What to look for ──
//
//   The machine processes items in a workflow. Midway through, we simulate
//   a crash. The machine is destroyed and recreated from a snapshot.
//   All domain data (progress, results) survives the interruption.
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Recovery;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.RecoveryDemo;

class Program
{
    // Item values for the batch — defined once, used in both the definition and restoration
    static readonly int[] ItemValues = { 150, 320, 85, 410, 200, 175, 290 };

    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     CleanState Sample: Recovery Demo                ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  A batch processing workflow that gets interrupted  ║");
        Console.WriteLine("║  mid-flow, then recovers from a checkpoint.         ║");
        Console.WriteLine("║                                                     ║");
        Console.WriteLine("║  Most FSMs break here. CleanState doesn't.          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build the workflow ──

        var definition = BuildWorkflowDefinition();

        // ══════════════════════════════════════════════════════
        // PHASE 1: Run the workflow until we "crash"
        // ══════════════════════════════════════════════════════

        Print("PHASE 1", "Starting workflow — will crash midway through", ConsoleColor.Cyan);
        Console.WriteLine(new string('─', 60));

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

        var itemReady = MachineBuilder.EventIdFrom(definition, "ItemReady");

        float time = 0f;
        machine.Start(time);
        scheduler.Update(time);

        MachineSnapshot snapshot = null;
        int crashAfterItems = 3;

        while (machine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            if (machine.Status == MachineStatus.Blocked && machine.BlockReason == BlockKind.WaitForEvent)
            {
                // Check if it's time to "crash"
                var processed = machine.Context.TryGet<int>("processedCount", out var count) ? count : 0;

                if (processed >= crashAfterItems && snapshot != null)
                {
                    // CRASH!
                    Console.WriteLine();
                    Print("CRASH", "╔══════════════════════════════╗", ConsoleColor.Red);
                    Print("CRASH", "║   SIMULATED POWER FAILURE!   ║", ConsoleColor.Red);
                    Print("CRASH", "║   Application terminated.    ║", ConsoleColor.Red);
                    Print("CRASH", "╚══════════════════════════════╝", ConsoleColor.Red);
                    Console.WriteLine();

                    Print("CRASH", $"Items processed before crash: {processed}", ConsoleColor.Red);
                    Print("CRASH", $"Total value before crash: ${machine.Context.Get<int>("totalValue")}", ConsoleColor.Red);
                    break;
                }

                // Capture snapshot at every checkpoint we hit
                if (machine.Context.Has("processedCount"))
                {
                    snapshot = MachineRecovery.CaptureSnapshot(machine,
                        "processedCount", "totalValue", "totalItems");
                    Print("SAVE", $"Checkpoint captured (processed: {machine.Context.Get<int>("processedCount")})", ConsoleColor.DarkMagenta);
                }

                machine.SendEvent(itemReady, time);
            }

            scheduler.Update(time);
        }

        if (machine.Status == MachineStatus.Completed)
        {
            Print("INFO", "Workflow completed without interruption.");
            return;
        }

        // ── Serialize the snapshot (simulating persistence) ──

        Print("PERSIST", "Serializing snapshot to JSON...", ConsoleColor.DarkCyan);
        var json = SnapshotSerializer.ToJson(snapshot);
        Print("PERSIST", $"Snapshot size: {json.Length} bytes", ConsoleColor.DarkCyan);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(json);
        Console.ResetColor();

        // ══════════════════════════════════════════════════════
        // PHASE 2: Recover from snapshot
        // ══════════════════════════════════════════════════════

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("PHASE 2", "Application restarting...", ConsoleColor.Cyan);
        Print("PHASE 2", "Loading snapshot from persistence...", ConsoleColor.Cyan);
        Console.WriteLine(new string('─', 60));

        // Deserialize — SnapshotSerializer handles the JsonElement → CLR type conversion
        var restoredSnapshot = SnapshotSerializer.FromJson(json);

        // Create a BRAND NEW scheduler and machine — as if the app just restarted
        var newTraceBuffer = new TraceBuffer(64);
        var newScheduler = new Scheduler();
        var newMachine = newScheduler.CreateMachine(definition, newTraceBuffer);

        newMachine.OnTransition += trace =>
        {
            var from = definition.NameLookup.GetStateName(trace.FromState);
            var to = definition.NameLookup.GetStateName(trace.ToState);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  ── {from} → {to} | {trace.Reason}: {trace.Detail}");
            Console.ResetColor();
        };

        // ── Restore from snapshot ──

        Print("RESTORE", "Restoring machine from snapshot...", ConsoleColor.Green);

        // Re-inject the item values array (not serialized — it's static config, not domain state)
        newMachine.Context.Set("itemValues", ItemValues);

        MachineRecovery.RestoreFromSnapshot(newMachine, restoredSnapshot, time);

        var restoredProcessed = newMachine.Context.Get<int>("processedCount");
        var restoredTotal = newMachine.Context.Get<int>("totalValue");
        Print("RESTORE", $"Restored at: {restoredProcessed} items processed, ${restoredTotal} total", ConsoleColor.Green);
        Print("RESTORE", "Resuming execution...", ConsoleColor.Green);
        Console.WriteLine(new string('─', 60));

        // ── Continue running ──

        var newItemReady = MachineBuilder.EventIdFrom(definition, "ItemReady");

        while (newMachine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            if (newMachine.Status == MachineStatus.Blocked && newMachine.BlockReason == BlockKind.WaitForEvent)
            {
                newMachine.SendEvent(newItemReady, time);
            }

            newScheduler.Update(time);
        }

        // ── Final results ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("COMPLETE", "Workflow finished after recovery!", ConsoleColor.Green);

        var finalProcessed = newMachine.Context.Get<int>("processedCount");
        var finalTotal = newMachine.Context.Get<int>("totalValue");
        Print("COMPLETE", $"Total items processed: {finalProcessed}", ConsoleColor.Green);
        Print("COMPLETE", $"Total value: ${finalTotal}", ConsoleColor.Green);

        Console.WriteLine();
        Print("PROOF", "The machine was destroyed and recreated.", ConsoleColor.Yellow);
        Print("PROOF", "Domain data survived the interruption.", ConsoleColor.Yellow);
        Print("PROOF", "Execution resumed from the last checkpoint.", ConsoleColor.Yellow);
        Print("PROOF", "No data loss. No restart. No hacks.", ConsoleColor.Yellow);

        // ── Timeline ──

        Console.WriteLine();
        Print("TIMELINE", "Post-recovery transitions:", ConsoleColor.Yellow);
        Console.WriteLine(new string('─', 60));

        var traces = newTraceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = definition.NameLookup.GetStateName(t.FromState);
            var to = definition.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} → {to}  ({t.Reason}: {t.Detail})");
        }
        Console.WriteLine(new string('═', 60));
    }

    static MachineDefinition BuildWorkflowDefinition()
    {
        return new MachineBuilder("BatchProcessor")

            .State("Initialize")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    ctx.Set("totalItems", ItemValues.Length);
                    ctx.Set("processedCount", 0);
                    ctx.Set("totalValue", 0);
                    ctx.Set("itemValues", ItemValues);
                    Print("INIT", $"Batch initialized: {ItemValues.Length} items to process");
                }, "InitBatch")
                .GoTo("ProcessItem", "GoToFirstItem")

            .State("ProcessItem")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var processed = ctx.Get<int>("processedCount");
                    var total = ctx.Get<int>("totalItems");
                    Print("PROCESS", $"Ready to process item {processed + 1}/{total}");
                }, "BeginProcessing")
                .WaitForEvent("ItemReady", "WaitForItem")
                .Then(ctx =>
                {
                    var processed = ctx.Get<int>("processedCount");
                    var values = ctx.Get<int[]>("itemValues");
                    var value = values[processed];

                    ctx.Set("totalValue", ctx.Get<int>("totalValue") + value);
                    ctx.Set("processedCount", processed + 1);

                    Print("PROCESS", $"Item {processed + 1}: value=${value} (running total: ${ctx.Get<int>("totalValue")})");
                }, "ProcessCurrentItem")
                .WaitForTime(0.3f, "ProcessingDelay")
                .Then(ctx =>
                {
                    Print("PROCESS", $"Item {ctx.Get<int>("processedCount")} committed.", ConsoleColor.DarkGreen);
                }, "CommitItem")
                .Decision("MoreItems")
                    .When(ctx => ctx.Get<int>("processedCount") >= ctx.Get<int>("totalItems"), "Finalize", "AllDone")
                    .Otherwise("ProcessItem", "MoreToProcess")

            .State("Finalize")
                .TransitionIn(ctx =>
                {
                    var total = ctx.Get<int>("totalValue");
                    var count = ctx.Get<int>("processedCount");
                    Console.WriteLine();
                    Print("FINAL", $"All {count} items processed. Total value: ${total}", ConsoleColor.Cyan);
                }, "ShowFinalReport")
            .Build();
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
