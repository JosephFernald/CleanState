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
        /// <summary>Unique identifier for this machine instance.</summary>
        MachineId Id { get; }
        /// <summary>Current execution status of the machine.</summary>
        MachineStatus Status { get; }
        /// <summary>The active state identifier.</summary>
        StateId CurrentState { get; }
        /// <summary>Index of the current step within the active state.</summary>
        int CurrentStepIndex { get; }
        /// <summary>The kind of block keeping the machine from progressing, if any.</summary>
        BlockKind BlockReason { get; }
        /// <summary>The compiled definition this machine executes.</summary>
        MachineDefinition Definition { get; }
        /// <summary>Captures the current machine state as an immutable debug snapshot.</summary>
        DebugSnapshot GetDebugSnapshot();
    }
}