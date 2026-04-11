# UIFlow

A multi-step onboarding flow demonstrating event-driven UI orchestration without booleans, coroutines, or Update loops.

## What This Demonstrates

- Sequential state orchestration (Welcome → Permissions → Preferences → Profile → Complete)
- Event-driven screen transitions (`UserTapped`, `PermissionResponse`, `PreferencesSubmitted`)
- Conditional branching (permissions granted vs. skipped → nudge screen)
- Async simulation (`WaitForTime` for API call with loading screen)
- Zero boolean flags — all flow state lives in the machine

## Problem It Solves

Most UI flows become a tangle of boolean flags and scattered state:

```csharp
bool isWaiting;
bool isAnimating;
bool hasConfirmed;
bool permissionsGranted;
int currentStep;
```

Nobody can tell what step the user is on, what they chose, or why the flow is stuck. Coroutine-based flows split execution across frames with no explicit control flow.

CleanState makes each screen a named state, each wait an explicit step, and each branch a declarative decision.

## How to Run

```bash
dotnet run --project samples/UIFlow/UIFlow.csproj
```

## What to Look For

- **Screen rendering** — each state draws an ASCII UI box showing what the user sees
- **Event simulation** — watch `[INPUT]` lines show which event is being simulated
- **Permission branching** — the flow takes different paths based on the user's permission choice
- **API call simulation** — `ProfileSetup` state blocks for 2 seconds with a loading screen
- **Transition log** — shows `DecisionBranch: PermissionDecision` with the exact branch taken
- **Final state** — shows the user's interest and notification status, proving data flowed through the machine
