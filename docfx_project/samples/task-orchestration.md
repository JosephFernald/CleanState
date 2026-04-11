# Task Orchestration

A document processing pipeline with validation, external service calls, retry logic with backoff, and timeout handling. This sample proves CleanState is not just for games — it's a general-purpose orchestration engine.

## The Problem

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

No visibility into which retry you're on. No recovery if the process dies. No trace of what happened.

## What This Sample Shows

- **Input validation** with pass/fail branching
- **Processing phase** with simulated work (timed wait)
- **External service call** that fails twice, then succeeds on the third attempt
- **Retry logic** with explicit retry counting and backoff delays
- **Timeout/failure states** when retries are exhausted
- **Checkpoint-based recovery** at each stable phase
- **Full pipeline traceability** — every phase, every retry, every decision

## State Flow

```
Validate ──┬── Process → CallService ──┬── Finalize
           │                           │
           └── Failed                  └── RetryCheck ──┬── Backoff → CallService (retry)
                                                        │
                                                        └── TimedOut
```

## The Machine Definition

[!code-csharp[Machine Definition](../../samples/TaskOrchestration/Program.cs#L56-L174 "Document pipeline definition")]

## Key Moments

### Retry with Backoff

The retry pattern is fully explicit — no hidden loop variables:

```text
[SERVICE ] Calling external service (attempt 1/3)...
[SERVICE ] Service responded: FAILED (TIMEOUT)
[RETRY   ] Retry 1/3 — checking if retries remain...
[BACKOFF ] Waiting 0.5s before retry...
[SERVICE ] Calling external service (attempt 2/3)...
[SERVICE ] Service responded: FAILED (502 BAD GATEWAY)
[RETRY   ] Retry 2/3 — checking if retries remain...
[BACKOFF ] Waiting 1.0s before retry...
[SERVICE ] Calling external service (attempt 3/3)...
[SERVICE ] Service responded: OK (ref: SVC-9529)
```

### Pipeline Trace

Every phase transition is recorded:

```text
[ 1] Validate → Process        (DecisionBranch: ValidationDecision)
[ 2] Process → CallService     (Direct: GoToCallService)
[ 3] CallService → RetryCheck  (DecisionBranch: ServiceDecision)
[ 4] RetryCheck → Backoff      (DecisionBranch: RetryDecision)
[ 5] Backoff → CallService     (Direct: RetryServiceCall)
...
[ 9] CallService → Finalize    (DecisionBranch: ServiceDecision)
```

## Running It

```bash
dotnet run --project samples/TaskOrchestration/TaskOrchestration.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/TaskOrchestration/Program.cs "Task Orchestration — full source")]
