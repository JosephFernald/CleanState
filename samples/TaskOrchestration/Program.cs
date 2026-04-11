// =============================================================================
// CleanState Sample: Background Job / Task Orchestration
// =============================================================================
//
// This sample proves CleanState is not just for games — it's a general-purpose
// orchestration engine for any system with complex, multi-step workflows.
//
// ── The scenario ──
//
//   A document processing pipeline:
//   1. Validate the input payload
//   2. Process the document (with simulated work)
//   3. Call an external service and wait for a callback
//   4. If the service fails, retry up to 3 times with backoff
//   5. If retries are exhausted, enter a timeout/failure state
//   6. On success, finalize and archive
//
// ── The typical approach ──
//
//   async Task ProcessDocument(Document doc) {
//       try {
//           await Validate(doc);
//           await Process(doc);
//           int retries = 0;
//           while (retries < 3) {
//               try { await CallService(doc); break; }
//               catch { retries++; await Task.Delay(retries * 1000); }
//           }
//           await Finalize(doc);
//       } catch { /* now what? */ }
//   }
//
//   No traceability. No recovery. Hidden retry state. Invisible flow.
//
// ── The CleanState approach ──
//
//   Every phase is a named state. Retries are explicit with visible counts.
//   The entire pipeline is traceable, recoverable, and debuggable.
//
// =============================================================================

using System;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.TaskOrchestration;

class Program
{
    static readonly Random Rng = new(77);

    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Task Orchestration            ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  A document processing pipeline with:               ║");
        Console.WriteLine("║  - Input validation                                 ║");
        Console.WriteLine("║  - Processing with simulated work                   ║");
        Console.WriteLine("║  - External service call with retry logic           ║");
        Console.WriteLine("║  - Timeout handling and failure states              ║");
        Console.WriteLine("║  - Full traceability across every phase             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build the pipeline ──

        var definition = new MachineBuilder("DocumentPipeline")

            .State("Validate")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    // Simulate receiving a document payload
                    ctx.Set("docId", "DOC-2024-0847");
                    ctx.Set("docSize", 2048);
                    ctx.Set("docType", "invoice");
                    ctx.Set("retryCount", 0);
                    ctx.Set("maxRetries", 3);

                    Print("VALIDATE", "Received document payload");
                    Print("VALIDATE", $"  ID:   {ctx.Get<string>("docId")}");
                    Print("VALIDATE", $"  Type: {ctx.Get<string>("docType")}");
                    Print("VALIDATE", $"  Size: {ctx.Get<int>("docSize")} bytes");
                }, "ReceivePayload")
                .Then(ctx =>
                {
                    // Validation checks
                    var size = ctx.Get<int>("docSize");
                    var docType = ctx.Get<string>("docType");
                    bool valid = size > 0 && size < 10_000_000
                        && (docType == "invoice" || docType == "receipt" || docType == "contract");
                    ctx.Set("isValid", valid);

                    if (valid)
                        Print("VALIDATE", "Validation passed.", ConsoleColor.Green);
                    else
                        Print("VALIDATE", "Validation FAILED.", ConsoleColor.Red);
                }, "RunValidation")
                .Decision("ValidationDecision")
                    .When(ctx => !ctx.Get<bool>("isValid"), "Failed", "InvalidPayload")
                    .Otherwise("Process", "PayloadValid")

            .State("Process")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var docId = ctx.Get<string>("docId");
                    Print("PROCESS", $"Processing document {docId}...");
                }, "BeginProcessing")
                .WaitForTime(1.0f, "ProcessingWork")
                .Then(ctx =>
                {
                    // Simulate OCR / parsing / extraction
                    ctx.Set("extractedFields", 12);
                    ctx.Set("confidence", 0.94f);
                    Print("PROCESS", $"Extracted {ctx.Get<int>("extractedFields")} fields (confidence: {ctx.Get<float>("confidence"):P0})", ConsoleColor.Green);
                }, "ExtractData")
                .GoTo("CallService", "GoToCallService")

            .State("CallService")
                .TransitionIn(ctx =>
                {
                    var attempt = ctx.Get<int>("retryCount") + 1;
                    var max = ctx.Get<int>("maxRetries");
                    Print("SERVICE", $"Calling external service (attempt {attempt}/{max})...", ConsoleColor.Cyan);
                }, "InitiateServiceCall")
                .WaitForEvent("ServiceResponse", "WaitForServiceResponse")
                .Then(ctx =>
                {
                    // Simulate service response — fails on first two attempts
                    var attempt = ctx.Get<int>("retryCount");
                    bool success = attempt >= 2; // Succeeds on 3rd attempt
                    ctx.Set("serviceSuccess", success);

                    if (success)
                    {
                        ctx.Set("serviceRef", $"SVC-{Rng.Next(10000):D4}");
                        Print("SERVICE", $"Service responded: OK (ref: {ctx.Get<string>("serviceRef")})", ConsoleColor.Green);
                    }
                    else
                    {
                        var errors = new[] { "TIMEOUT", "502 BAD GATEWAY", "CONNECTION RESET" };
                        var error = errors[attempt % errors.Length];
                        ctx.Set("lastError", error);
                        Print("SERVICE", $"Service responded: FAILED ({error})", ConsoleColor.Red);
                    }
                }, "HandleServiceResponse")
                .Decision("ServiceDecision")
                    .When(ctx => ctx.Get<bool>("serviceSuccess"), "Finalize", "ServiceOK")
                    .Otherwise("RetryCheck", "ServiceFailed")

            .State("RetryCheck")
                .TransitionIn(ctx =>
                {
                    var count = ctx.Get<int>("retryCount") + 1;
                    ctx.Set("retryCount", count);

                    var max = ctx.Get<int>("maxRetries");
                    Print("RETRY", $"Retry {count}/{max} — checking if retries remain...");
                }, "IncrementRetry")
                .Decision("RetryDecision")
                    .When(ctx => ctx.Get<int>("retryCount") >= ctx.Get<int>("maxRetries"), "TimedOut", "RetriesExhausted")
                    .Otherwise("Backoff", "WillRetry")

            .State("Backoff")
                .TransitionIn(ctx =>
                {
                    var count = ctx.Get<int>("retryCount");
                    var delay = count * 0.5f;
                    ctx.Set("backoffDelay", delay);
                    Print("BACKOFF", $"Waiting {delay:F1}s before retry...", ConsoleColor.DarkYellow);
                }, "CalculateBackoff")
                .WaitForTime(0.5f, "BackoffWait")
                .GoTo("CallService", "RetryServiceCall")

            .State("Finalize")
                .Checkpoint()
                .TransitionIn(ctx =>
                {
                    var docId = ctx.Get<string>("docId");
                    var svcRef = ctx.Get<string>("serviceRef");
                    var fields = ctx.Get<int>("extractedFields");
                    var retries = ctx.Get<int>("retryCount");

                    Console.WriteLine();
                    Print("FINALIZE", "Pipeline complete.", ConsoleColor.Green);
                    Print("FINALIZE", $"  Document:     {docId}");
                    Print("FINALIZE", $"  Service Ref:  {svcRef}");
                    Print("FINALIZE", $"  Fields:       {fields}");
                    Print("FINALIZE", $"  Retries used: {retries}");
                    Print("FINALIZE", "  Status:       ARCHIVED", ConsoleColor.Green);
                }, "ArchiveDocument")

            .State("TimedOut")
                .TransitionIn(ctx =>
                {
                    var docId = ctx.Get<string>("docId");
                    var lastError = ctx.Get<string>("lastError");
                    Console.WriteLine();
                    Print("TIMEOUT", $"Pipeline FAILED for {docId}", ConsoleColor.Red);
                    Print("TIMEOUT", $"  Last error: {lastError}", ConsoleColor.Red);
                    Print("TIMEOUT", "  Status: DEAD LETTER QUEUE", ConsoleColor.Red);
                }, "MoveToDeadLetter")

            .State("Failed")
                .TransitionIn(ctx =>
                {
                    var docId = ctx.Get<string>("docId");
                    Console.WriteLine();
                    Print("FAILED", $"Validation failed for {docId}", ConsoleColor.Red);
                    Print("FAILED", "  Status: REJECTED", ConsoleColor.Red);
                }, "RejectDocument")

            .Build();

        // ── Run the pipeline ──

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

        var serviceResponse = MachineBuilder.EventIdFrom(definition, "ServiceResponse");

        Print("ENGINE", "Starting document pipeline...");
        Console.WriteLine(new string('─', 60));

        float time = 0f;
        machine.Start(time);
        scheduler.Update(time);

        while (machine.Status != MachineStatus.Completed)
        {
            time += 0.1f;

            if (machine.Status == MachineStatus.Blocked && machine.BlockReason == BlockKind.WaitForEvent)
            {
                // Simulate external service callback after a short delay
                time += 0.3f;
                Print("CALLBACK", "External service callback received.", ConsoleColor.DarkGray);
                machine.SendEvent(serviceResponse, time);
            }

            scheduler.Update(time);
        }

        // ── Timeline ──

        Console.WriteLine();
        Console.WriteLine(new string('═', 60));
        Print("TIMELINE", "Full pipeline trace:", ConsoleColor.Yellow);
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

        var snap = machine.GetDebugSnapshot();
        Console.WriteLine();
        Print("RESULT", $"Machine: {snap.MachineName} — {snap.Status}");
        Print("RESULT", $"Final state: {snap.CurrentStateName}");
        Print("RESULT", "Every phase traced. Every retry visible. Fully recoverable.");
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
