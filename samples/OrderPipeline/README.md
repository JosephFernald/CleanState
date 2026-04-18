# OrderPipeline

A three-phase e-commerce order pipeline where each phase is a child sub-state machine — validation, payment, and fulfillment — orchestrated by a parent machine using `RunChild`.

## What This Demonstrates

- `RunChild` — parent spawns a child machine and blocks until it completes
- `childInit` callback — parent passes order data (orderId, items, amount, address) into each child's context before it starts
- Three independently built child definitions, each with 3 internal states:
  - **Validation**: CheckInventory → VerifyAddress → ConfirmAccount
  - **Payment**: Authorize → Hold → Capture
  - **Fulfillment**: Pick → Pack → Ship
- Parent pipeline has 5 clean states (Receive, Validate, Pay, Fulfill, Complete) — zero knowledge of child internals
- Children use timed waits to simulate real async work (inventory lookup, gateway authorization, warehouse picking)
- Sequential child execution — each phase completes before the next begins
- Child machines are automatically removed from the scheduler after completion

## Problem It Solves

Complex workflows decompose into sub-workflows. Without child machines, you either flatten everything into a monolithic state machine or scatter logic across async methods:

```csharp
async Task ProcessOrder(Order order) {
    await ValidateInventory(order);   // where does this state live?
    await ValidateAddress(order);     // what if it fails halfway?
    await AuthorizePayment(order);    // retry logic scattered
    await CapturePayment(order);      // no visibility into sub-steps
    await PickAndPack(order);         // impossible to recover
    await Ship(order);               // no traceability
}
```

No sub-step visibility. No recovery. No trace of what happened inside each phase. With `RunChild`, the parent stays simple (5 states) and each child is testable and traceable in isolation.

## How to Run

```bash
dotnet run --project samples/OrderPipeline/OrderPipeline.csproj
```

## What to Look For

- **Phase announcements** — `[PIPELINE]` lines mark each phase starting and completing
- **Indented child output** — `[  VALID]`, `[  PAY]`, `[  SHIP]` lines show the child machine's internal progress
- **Data passing** — child machines receive order data via `childInit` (inventory count, payment method, warehouse ID)
- **Timed sub-steps** — each child state includes a simulated wait (inventory lookup 1.0s, gateway auth 1.5s, picking 1.5s, etc.)
- **Parent timeline** — 4 transitions spanning the full 9.5s of simulated time across all three child executions
- **Clean completion** — parent finishes only after all three children have completed their full internal lifecycles
