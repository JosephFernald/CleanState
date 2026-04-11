// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// A branch in a decision. Evaluated in order — first match wins.
    /// </summary>
    public sealed class DecisionBranch
    {
        public Func<MachineContext, bool> Condition { get; }
        public StateId TargetState { get; }
        public string Label { get; }

        public DecisionBranch(Func<MachineContext, bool> condition, StateId targetState, string label)
        {
            Condition = condition;
            TargetState = targetState;
            Label = label;
        }
    }

    /// <summary>
    /// Evaluates branches in order and transitions to the first matching target.
    /// The last branch should be the "otherwise" (always-true) fallback.
    /// </summary>
    public sealed class DecisionStep : IStep
    {
        private readonly DecisionBranch[] _branches;

        public StepDebugInfo DebugInfo { get; }

        /// <summary>
        /// Read-only access to branches for visualization/tooling.
        /// </summary>
        public DecisionBranch[] GetBranches() => _branches;

        public DecisionStep(DecisionBranch[] branches, StepDebugInfo debugInfo)
        {
            _branches = branches ?? throw new ArgumentNullException(nameof(branches));
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));

            if (_branches.Length == 0)
                throw new ArgumentException("Decision must have at least one branch.", nameof(branches));
        }

        public StepResult Execute(MachineContext context)
        {
            for (int i = 0; i < _branches.Length; i++)
            {
                if (_branches[i].Condition(context))
                    return StepResult.Transition(_branches[i].TargetState);
            }

            // Should not reach here if builder ensures an Otherwise branch
            throw new InvalidOperationException(
                $"No decision branch matched at {DebugInfo}. Ensure an Otherwise branch is defined.");
        }
    }
}