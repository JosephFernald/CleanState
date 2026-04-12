// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;

namespace CleanState.Steps
{
    /// <summary>
    /// A single unit of execution within a state.
    /// </summary>
    public interface IStep
    {
        /// <summary>Debug metadata describing this step's origin.</summary>
        StepDebugInfo DebugInfo { get; }
        /// <summary>Executes this step and returns the result indicating what the machine should do next.</summary>
        StepResult Execute(MachineContext context);
    }
}