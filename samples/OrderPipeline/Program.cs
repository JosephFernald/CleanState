// =============================================================================
// CleanState Sample: Order Pipeline — Child / Sub-State Machines
// =============================================================================
//
// This sample demonstrates RunChild — spawning child machines that execute
// independently and block the parent until they complete.
//
// ── The scenario ──
//
//   An e-commerce order pipeline with three phases:
//
//   1. VALIDATION  — A child machine checks inventory, verifies the address,
//                    and confirms the customer account. Each check is a
//                    separate state with its own timing.
//
//   2. PAYMENT     — A child machine processes the charge: authorize,
//                    wait for gateway callback, capture funds.
//
//   3. FULFILLMENT — A child machine handles picking, packing, and shipping
//                    with a timed wait for warehouse confirmation.
//
//   The parent pipeline doesn't know the internal states of each child —
//   it just says "run this sub-workflow and continue when it's done."
//
// ── What this proves ──
//
//   - RunChild spawns a child machine and blocks until it completes
//   - childInit passes data from parent context into the child
//   - Each child has its own states, transitions, and trace history
//   - Children can use events, time waits, predicates, and decisions
//   - The parent continues exactly where it left off after each child
//   - Multiple sequential children compose into a clean pipeline
//   - Child definitions are reusable and independently testable
//
// ── The typical approach ──
//
//   async Task ProcessOrder(Order order) {
//       await ValidateInventory(order);   // where does this state live?
//       await ValidateAddress(order);     // what if it fails halfway?
//       await AuthorizePayment(order);    // retry logic scattered
//       await CapturePayment(order);      // no visibility into sub-steps
//       await PickAndPack(order);         // impossible to recover
//       await Ship(order);               // no traceability
//   }
//
//   No substep visibility, no recovery, no trace of what happened inside.
//
// =============================================================================

using System;
using System.Collections.Generic;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Samples.OrderPipeline;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║    CleanState Sample: Order Pipeline                ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Demonstrates RunChild (child sub-state machines):  ║");
        Console.WriteLine("║  - Validation child  (3 states)                     ║");
        Console.WriteLine("║  - Payment child     (3 states)                     ║");
        Console.WriteLine("║  - Fulfillment child (3 states)                     ║");
        Console.WriteLine("║  - Parent pipeline orchestrates all three           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── Build child definitions ──

        var validationDef = BuildValidation();
        var paymentDef    = BuildPayment();
        var fulfillmentDef = BuildFulfillment();

        // ── Build the parent pipeline ──

        var pipelineDef = new MachineBuilder("OrderPipeline")

            .State("Receive")
                .TransitionIn(ctx =>
                {
                    ctx.Set("orderId", "ORD-88421");
                    ctx.Set("customer", "Alice Chen");
                    ctx.Set("items", 3);
                    ctx.Set("total", 247.50);

                    Print("RECEIVE", "New order received.");
                    Print("RECEIVE", $"  Order:    {ctx.Get<string>("orderId")}");
                    Print("RECEIVE", $"  Customer: {ctx.Get<string>("customer")}");
                    Print("RECEIVE", $"  Items:    {ctx.Get<int>("items")}");
                    Print("RECEIVE", $"  Total:    ${ctx.Get<double>("total"):F2}");
                }, "ReceiveOrder")
                .GoTo("Validate", "StartValidation")

            .State("Validate")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("PIPELINE", "Phase 1: Validation", ConsoleColor.Cyan);
                    Print("PIPELINE", "  Spawning validation child machine...");
                }, "AnnounceValidation")
                .RunChild(validationDef, "RunValidation",
                    childInit: ctx =>
                    {
                        // Pass order data into the child's context
                        ctx.Set("orderId", "ORD-88421");
                        ctx.Set("items", 3);
                        ctx.Set("address", "742 Evergreen Terrace");
                        ctx.Set("customerId", "CUST-1042");
                    })
                .Then(ctx =>
                {
                    Print("PIPELINE", "  Validation child completed.", ConsoleColor.Green);
                }, "ValidationDone")
                .GoTo("Pay", "StartPayment")

            .State("Pay")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("PIPELINE", "Phase 2: Payment", ConsoleColor.Cyan);
                    Print("PIPELINE", "  Spawning payment child machine...");
                }, "AnnouncePayment")
                .RunChild(paymentDef, "RunPayment",
                    childInit: ctx =>
                    {
                        ctx.Set("orderId", "ORD-88421");
                        ctx.Set("amount", 247.50);
                        ctx.Set("method", "Visa ending 4242");
                    })
                .Then(ctx =>
                {
                    Print("PIPELINE", "  Payment child completed.", ConsoleColor.Green);
                }, "PaymentDone")
                .GoTo("Fulfill", "StartFulfillment")

            .State("Fulfill")
                .TransitionIn(ctx =>
                {
                    Console.WriteLine();
                    Print("PIPELINE", "Phase 3: Fulfillment", ConsoleColor.Cyan);
                    Print("PIPELINE", "  Spawning fulfillment child machine...");
                }, "AnnounceFulfillment")
                .RunChild(fulfillmentDef, "RunFulfillment",
                    childInit: ctx =>
                    {
                        ctx.Set("orderId", "ORD-88421");
                        ctx.Set("items", 3);
                        ctx.Set("warehouse", "WH-EAST-07");
                    })
                .Then(ctx =>
                {
                    Print("PIPELINE", "  Fulfillment child completed.", ConsoleColor.Green);
                }, "FulfillmentDone")
                .GoTo("Complete", "Finalize")

            .State("Complete")
                .TransitionIn(ctx =>
                {
                    var orderId = ctx.Get<string>("orderId");
                    Console.WriteLine();
                    Print("COMPLETE", "========================================", ConsoleColor.Green);
                    Print("COMPLETE", $"  ORDER {orderId} SHIPPED", ConsoleColor.Green);
                    Print("COMPLETE", "  All three child machines completed.", ConsoleColor.Green);
                    Print("COMPLETE", "========================================", ConsoleColor.Green);
                }, "AnnounceComplete")

            .Build();

        // ── Run the pipeline ──

        var traceBuffer = new TraceBuffer(32);
        var scheduler = new Scheduler();
        var machine = scheduler.CreateMachine(pipelineDef, traceBuffer);

        machine.OnTransition += trace =>
        {
            var from = pipelineDef.NameLookup.GetStateName(trace.FromState);
            var to = pipelineDef.NameLookup.GetStateName(trace.ToState);
            Print("TRANS", $"{from} -> {to}  ({trace.Detail})", ConsoleColor.DarkGray);
        };

        double time = 0.0;
        machine.Start(time);

        // Tick until pipeline completes
        while (machine.Status != MachineStatus.Completed && machine.Status != MachineStatus.Faulted)
        {
            time += 0.5;
            scheduler.Update(time);
        }

        // ── Print parent timeline ──

        Console.WriteLine();
        Print("TIMELINE", "Parent pipeline transitions:", ConsoleColor.Yellow);
        Console.WriteLine(new string('-', 55));

        var traces = traceBuffer.GetTraces();
        for (int i = 0; i < traces.Length; i++)
        {
            var t = traces[i];
            var from = pipelineDef.NameLookup.GetStateName(t.FromState);
            var to = pipelineDef.NameLookup.GetStateName(t.ToState);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"    [{i + 1}] ");
            Console.ResetColor();
            Console.WriteLine($"{from} -> {to}  (t={t.Timestamp:F1}s, {t.Detail})");
        }

        Print("RESULT", $"Final: {machine.Status} at t={time:F1}s", ConsoleColor.White);
    }

    // ── Child machine definitions ───────────────────────────────

    static MachineDefinition BuildValidation()
    {
        return new MachineBuilder("Validation")

            .State("CheckInventory")
                .TransitionIn(ctx =>
                {
                    var items = ctx.Get<int>("items");
                    Print("  VALID", $"Checking inventory for {items} items...", ConsoleColor.DarkCyan);
                }, "StartInventoryCheck")
                .WaitForTime(1.0, "InventoryLookup")
                .Then(ctx =>
                {
                    ctx.Set("inventoryOk", true);
                    Print("  VALID", "  Inventory confirmed.", ConsoleColor.Green);
                }, "InventoryResult")
                .GoTo("VerifyAddress", "NextCheck")

            .State("VerifyAddress")
                .TransitionIn(ctx =>
                {
                    var addr = ctx.Get<string>("address");
                    Print("  VALID", $"Verifying address: {addr}", ConsoleColor.DarkCyan);
                }, "StartAddressCheck")
                .WaitForTime(0.5, "AddressLookup")
                .Then(ctx =>
                {
                    ctx.Set("addressOk", true);
                    Print("  VALID", "  Address verified.", ConsoleColor.Green);
                }, "AddressResult")
                .GoTo("ConfirmAccount", "NextCheck")

            .State("ConfirmAccount")
                .TransitionIn(ctx =>
                {
                    var custId = ctx.Get<string>("customerId");
                    Print("  VALID", $"Confirming account {custId}...", ConsoleColor.DarkCyan);
                }, "StartAccountCheck")
                .WaitForTime(0.5, "AccountLookup")
                .Then(ctx =>
                {
                    Print("  VALID", "  Account confirmed. All checks passed.", ConsoleColor.Green);
                }, "AccountResult")

            .Build();
    }

    static MachineDefinition BuildPayment()
    {
        return new MachineBuilder("Payment")

            .State("Authorize")
                .TransitionIn(ctx =>
                {
                    var amount = ctx.Get<double>("amount");
                    var method = ctx.Get<string>("method");
                    Print("  PAY", $"Authorizing ${amount:F2} on {method}...", ConsoleColor.DarkCyan);
                }, "StartAuth")
                .WaitForTime(1.5, "GatewayAuth")
                .Then(ctx =>
                {
                    ctx.Set("authCode", "AUTH-77920");
                    Print("  PAY", $"  Authorization granted: {ctx.Get<string>("authCode")}", ConsoleColor.Green);
                }, "AuthResult")
                .GoTo("Hold", "ProceedToHold")

            .State("Hold")
                .TransitionIn(ctx =>
                {
                    Print("  PAY", "  Placing hold on funds...", ConsoleColor.DarkCyan);
                }, "StartHold")
                .WaitForTime(0.5, "HoldPeriod")
                .Then(ctx =>
                {
                    Print("  PAY", "  Hold confirmed.", ConsoleColor.Green);
                }, "HoldResult")
                .GoTo("Capture", "ProceedToCapture")

            .State("Capture")
                .TransitionIn(ctx =>
                {
                    var auth = ctx.Get<string>("authCode");
                    Print("  PAY", $"  Capturing funds (auth: {auth})...", ConsoleColor.DarkCyan);
                }, "StartCapture")
                .WaitForTime(1.0, "CaptureDelay")
                .Then(ctx =>
                {
                    ctx.Set("captureRef", "CAP-30815");
                    Print("  PAY", $"  Funds captured: {ctx.Get<string>("captureRef")}", ConsoleColor.Green);
                }, "CaptureResult")

            .Build();
    }

    static MachineDefinition BuildFulfillment()
    {
        return new MachineBuilder("Fulfillment")

            .State("Pick")
                .TransitionIn(ctx =>
                {
                    var items = ctx.Get<int>("items");
                    var wh = ctx.Get<string>("warehouse");
                    Print("  SHIP", $"Picking {items} items from {wh}...", ConsoleColor.DarkCyan);
                }, "StartPick")
                .WaitForTime(1.5, "PickingTime")
                .Then(ctx =>
                {
                    Print("  SHIP", "  All items picked.", ConsoleColor.Green);
                }, "PickResult")
                .GoTo("Pack", "NextStep")

            .State("Pack")
                .TransitionIn(ctx =>
                {
                    Print("  SHIP", "  Packing order...", ConsoleColor.DarkCyan);
                }, "StartPack")
                .WaitForTime(1.0, "PackingTime")
                .Then(ctx =>
                {
                    ctx.Set("trackingNo", "1Z-999-AA1-012-345-678");
                    Print("  SHIP", "  Packed. Label printed.", ConsoleColor.Green);
                }, "PackResult")
                .GoTo("Ship", "NextStep")

            .State("Ship")
                .TransitionIn(ctx =>
                {
                    var tracking = ctx.Get<string>("trackingNo");
                    Print("  SHIP", $"  Handing off to carrier...", ConsoleColor.DarkCyan);
                    Print("  SHIP", $"  Tracking: {tracking}", ConsoleColor.DarkCyan);
                }, "StartShip")
                .WaitForTime(0.5, "CarrierHandoff")
                .Then(ctx =>
                {
                    Print("  SHIP", "  Shipment confirmed. In transit.", ConsoleColor.Green);
                }, "ShipResult")

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
