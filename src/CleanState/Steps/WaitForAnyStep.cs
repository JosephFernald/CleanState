// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until ANY sub-condition is satisfied.
    /// Stores time targets in <see cref="MachineContext"/> so multiple machines
    /// can safely share the same definition.
    /// </summary>
    public sealed class WaitForAnyStep : IStep
    {
        private readonly CompositeCondition[] _conditions;
        private readonly string _contextPrefix;

        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>The sub-conditions, any one of which will unblock the step.</summary>
        public CompositeCondition[] Conditions => _conditions;

        /// <summary>Creates a WaitForAnyStep that blocks until at least one condition is met.</summary>
        public WaitForAnyStep(CompositeCondition[] conditions, StepDebugInfo debugInfo)
        {
            if (conditions == null || conditions.Length == 0)
                throw new ArgumentException("WaitForAny requires at least one condition.", nameof(conditions));
            _conditions = conditions;
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));
            _contextPrefix = $"__wfany_{debugInfo.StateName}_{debugInfo.StepIndex}";
        }

        /// <inheritdoc />
        public StepResult Execute(MachineContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                var cond = _conditions[i];

                switch (cond.Kind)
                {
                    case CompositeConditionKind.Event:
                        if (context.LastReceivedEvent == cond.EventId)
                        {
                            Cleanup(context);
                            return StepResult.Continue();
                        }
                        break;

                    case CompositeConditionKind.Time:
                        var timeKey = $"{_contextPrefix}_time_{i}";
                        if (!context.TryGet<double>(timeKey, out var targetTime))
                        {
                            targetTime = context.CurrentTime + cond.Duration;
                            context.Set(timeKey, targetTime);
                        }
                        if (context.CurrentTime >= targetTime)
                        {
                            Cleanup(context);
                            return StepResult.Continue();
                        }
                        break;

                    case CompositeConditionKind.Predicate:
                        if (cond.Predicate(context))
                        {
                            Cleanup(context);
                            return StepResult.Continue();
                        }
                        break;
                }
            }

            return StepResult.WaitForComposite();
        }

        private void Cleanup(MachineContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                if (_conditions[i].Kind == CompositeConditionKind.Time)
                    context.Remove($"{_contextPrefix}_time_{i}");
            }
        }
    }
}
