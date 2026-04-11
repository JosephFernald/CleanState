# TaskOrchestration

A document processing pipeline with validation, external service calls, retry logic with backoff, and timeout handling. Proves CleanState works beyond games.

## What This Demonstrates

- Input validation with pass/fail branching
- Processing phase with simulated OCR/extraction work
- External service call that fails twice, then succeeds on the third attempt
- Explicit retry counting with increasing backoff delays
- Timeout and dead letter queue when retries are exhausted
- Checkpoint-based recovery at each stable phase
- Full pipeline traceability — every phase, every retry, every decision

## Problem It Solves

Typical async pipeline code hides retry state and has no traceability:

```csharp
async Task ProcessDocument(Document doc) {
    await Validate(doc);
    await Process(doc);
    int retries = 0;
    while (retries < 3) {
        try { await CallService(doc); break; }
        catch { retries++; await Task.Delay(retries * 1000); }
    }
    await Finalize(doc);
}
```

No visibility into which retry you're on. No recovery if the process dies mid-retry. No trace of what happened after the fact.

CleanState makes every phase a named state, every retry a visible transition, and every failure a traceable decision.

## How to Run

```bash
dotnet run --project samples/TaskOrchestration/TaskOrchestration.csproj
```

## What to Look For

- **Validation gate** — the pipeline rejects invalid payloads before any work begins
- **Service failures** — watch `TIMEOUT` and `502 BAD GATEWAY` errors on the first two attempts
- **Retry counting** — `[RETRY]` lines show explicit `1/3`, `2/3` progress
- **Backoff delays** — 0.5s after first failure, 1.0s after second
- **Success on third attempt** — service returns OK with a reference number
- **Pipeline trace** — 9 transitions showing every phase and retry loop
- **Finalize report** — document ID, service ref, field count, and retries used
