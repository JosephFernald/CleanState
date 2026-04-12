// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;

namespace CleanState.Steps
{
    /// <summary>
    /// Executes an action immediately and continues.
    /// </summary>
    public sealed class ActionStep : IStep
    {
        private readonly Action<MachineContext> _action;

        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>Creates a new ActionStep that runs the specified action.</summary>
        public ActionStep(Action<MachineContext> action, StepDebugInfo debugInfo)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));
        }

        /// <inheritdoc />
        public StepResult Execute(MachineContext context)
        {
            _action(context);
            return StepResult.Continue();
        }
    }
}