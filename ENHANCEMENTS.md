# Future Enhancements

Tracked improvements and refactoring opportunities for CleanState.

## Core Library

### DebugSnapshot Constructor

The `DebugSnapshot` constructor takes 13 parameters, making it fragile and hard to extend. Refactor to use a builder pattern or a parameter object to improve readability and maintainability.

**File:** `src/CleanState/Debug/DebugSnapshot.cs`

**Current:**
```csharp
public DebugSnapshot(
    string machineName,
    MachineStatus status,
    string currentStateName,
    int currentStepIndex,
    BlockKind blockReason,
    EventId lastEvent,
    TransitionTrace lastTransition,
    EventId waitingForEvent,
    string waitingForEventName,
    float waitUntilTime,
    string currentStepLabel,
    string currentStepType,
    int stepCountInCurrentState)
```

**Options:**
- Introduce a `DebugSnapshotBuilder` with fluent API
- Use a parameter object / options struct
- Have `Machine.GetDebugSnapshot()` construct it internally without exposing the constructor publicly

### StepDebugInfo Constructor

Same issue as `DebugSnapshot` — the `StepDebugInfo` constructor takes 7 parameters. Should be simplified alongside the `DebugSnapshot` refactoring.

**File:** `src/CleanState/Debug/StepDebugInfo.cs`

**Current:**
```csharp
public StepDebugInfo(
    string machineName,
    string stateName,
    int stepIndex,
    string stepType,
    string label,
    string sourceFile = null,
    int sourceLine = 0)
```

**Options:**
- Use a parameter object / options struct
- Since `StepDebugInfo` is constructed internally by the builder during compilation, consider making the constructor internal and exposing a factory method
