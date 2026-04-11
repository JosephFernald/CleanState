// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// Immediately transitions to a target state. Used for direct GoTo transitions.
    /// </summary>
    public sealed class TransitionStep : IStep
    {
        public StateId TargetState { get; }
        public StepDebugInfo DebugInfo { get; }

        public TransitionStep(StateId targetState, StepDebugInfo debugInfo)
        {
            TargetState = targetState;
            DebugInfo = debugInfo;
        }

        public StepResult Execute(MachineContext context)
        {
            return StepResult.Transition(TargetState);
        }
    }
}