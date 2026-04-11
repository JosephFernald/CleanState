// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until a predicate returns true.
    /// Re-evaluated each time the machine is ticked.
    /// </summary>
    public sealed class WaitForPredicateStep : IStep
    {
        private readonly Func<MachineContext, bool> _predicate;

        public StepDebugInfo DebugInfo { get; }

        public WaitForPredicateStep(Func<MachineContext, bool> predicate, StepDebugInfo debugInfo)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));
        }

        public StepResult Execute(MachineContext context)
        {
            if (_predicate(context))
                return StepResult.Continue();

            return StepResult.WaitForPredicate();
        }
    }
}