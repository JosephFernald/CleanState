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
