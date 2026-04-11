# UI Flow

A multi-step onboarding flow demonstrating event-driven UI orchestration — no booleans, no coroutines, no spaghetti.

## The Problem

Most UI flows become a tangle of boolean flags:

```csharp
bool isWaiting;
bool isAnimating;
bool hasConfirmed;
bool permissionsGranted;
int currentStep;
```

Nobody can tell what step the user is on, what they chose, or why the flow is stuck.

## What This Sample Shows

- **Event-driven flow** — each screen waits for a specific user event, no Update loops
- **Conditional branching** — permissions granted vs. skipped leads to different paths
- **Async simulation** — `WaitForTime` simulates an API call with a loading screen
- **Step-level debugging** — every step is named and traceable
- **Zero boolean flags** — all flow state lives in the machine

## State Flow

```
Welcome → Permissions ──┬── UserPreferences → ProfileSetup → Complete
                        │
                        └── PermissionNudge → UserPreferences → ...
```

## The Machine Definition

[!code-csharp[Machine Definition](../../samples/UIFlow/Program.cs#L48-L161 "UI Flow machine definition")]

## Key Moments

### Event-Driven Screens

Each screen waits for a specific event — no polling, no frame-driven checks:

```csharp
.State("Welcome")
    .TransitionIn(ctx => ShowWelcomeScreen(), "ShowWelcomeScreen")
    .WaitForEvent("UserTapped", "WaitForWelcomeTap")
    .Then(ctx => HandleTap(), "AckWelcome")
    .GoTo("Permissions", "GoToPermissions")
```

### Conditional Branching

The permissions screen branches based on the user's choice:

```csharp
.Decision("PermissionDecision")
    .When(ctx => ctx.Get<bool>("permissionsGranted"), "UserPreferences", "PermissionsGranted")
    .Otherwise("PermissionNudge", "PermissionsSkipped")
```

The transition log shows exactly which path was taken:

```text
Permissions → UserPreferences  (DecisionBranch: PermissionDecision)
```

### Async API Simulation

The profile setup screen simulates an API call using `WaitForTime`:

```csharp
.State("ProfileSetup")
    .TransitionIn(ctx => ShowLoadingScreen(), "ShowLoadingScreen")
    .WaitForTime(2.0f, "SimulateApiCall")
    .Then(ctx => HandleResponse(), "HandleApiResponse")
    .GoTo("Complete", "GoToComplete")
```

No coroutines. No callbacks. The step is named `SimulateApiCall` — you know exactly what's blocking.

### Full Traceability

The output shows the complete flow:

```text
[1] Welcome → Permissions        (Direct: GoToPermissions) at t=0.6s
[2] Permissions → UserPreferences (DecisionBranch: PermissionDecision) at t=1.2s
[3] UserPreferences → ProfileSetup (Direct: GoToProfileSetup) at t=1.8s
[4] ProfileSetup → Complete       (Direct: GoToComplete) at t=3.9s
```

## Running It

```bash
dotnet run --project samples/UIFlow/UIFlow.csproj
```

## Full Source

[!code-csharp[Full Source](../../samples/UIFlow/Program.cs "UI Flow — full source")]
