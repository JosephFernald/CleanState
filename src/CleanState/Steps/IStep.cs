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
        StepDebugInfo DebugInfo { get; }
        StepResult Execute(MachineContext context);
    }
}