// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Identity;
using CleanState.Steps;

namespace CleanState.Builder
{
    /// <summary>
    /// Fluent builder for decision branches within a state.
    /// </summary>
    public sealed class DecisionBuilder
    {
        private readonly StateBuilder _parent;
        private readonly string _label;
        private readonly List<PendingBranch> _branches = new List<PendingBranch>();

        internal DecisionBuilder(StateBuilder parent, string label)
        {
            _parent = parent;
            _label = label;
        }

        /// <summary>
        /// Add a conditional branch: if condition is true, transition to targetState.
        /// </summary>
        public DecisionBuilder When(Func<MachineContext, bool> condition, string targetState, string label = null)
        {
            _branches.Add(new PendingBranch(condition, targetState, label ?? targetState));
            return this;
        }

        /// <summary>
        /// Add the fallback branch (always-true). Must be called last.
        /// Returns the parent StateBuilder to continue building.
        /// </summary>
        public StateBuilder Otherwise(string targetState, string label = null)
        {
            _branches.Add(new PendingBranch(_ => true, targetState, label ?? "Otherwise"));
            _parent.AddPendingDecision(_label, _branches);
            return _parent;
        }

        internal readonly struct PendingBranch
        {
            public readonly Func<MachineContext, bool> Condition;
            public readonly string TargetStateName;
            public readonly string Label;

            public PendingBranch(Func<MachineContext, bool> condition, string targetStateName, string label)
            {
                Condition = condition;
                TargetStateName = targetStateName;
                Label = label;
            }
        }
    }
}