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
        /// <summary>The condition that must be true for this branch to match.</summary>
        public Func<MachineContext, bool> Condition { get; }
        /// <summary>The state to transition to when this branch matches.</summary>
        public StateId TargetState { get; }
        /// <summary>A human-readable label for this branch, used in diagnostics.</summary>
        public string Label { get; }

        /// <summary>Creates a new decision branch with the specified condition, target, and label.</summary>
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

        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>Returns a read-only copy of branches for visualization and tooling.</summary>
        public DecisionBranch[] GetBranches() => _branches;

        /// <summary>Creates a new DecisionStep with the specified branches and debug info.</summary>
        public DecisionStep(DecisionBranch[] branches, StepDebugInfo debugInfo)
        {
            _branches = branches ?? throw new ArgumentNullException(nameof(branches));
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));

            if (_branches.Length == 0)
                throw new ArgumentException("Decision must have at least one branch.", nameof(branches));
        }

        /// <inheritdoc />
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