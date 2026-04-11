// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;
using CleanState.Identity;
using CleanState.Steps;

namespace CleanState.Runtime
{
    /// <summary>
    /// Read-only observation surface for a machine instance.
    /// This is the ONLY interface the editor/visualizer should consume.
    /// No mutation, no context access, no forcing transitions.
    /// </summary>
    public interface IFsmObservable
    {
        MachineId Id { get; }
        MachineStatus Status { get; }
        StateId CurrentState { get; }
        int CurrentStepIndex { get; }
        BlockKind BlockReason { get; }
        MachineDefinition Definition { get; }
        DebugSnapshot GetDebugSnapshot();
    }
}