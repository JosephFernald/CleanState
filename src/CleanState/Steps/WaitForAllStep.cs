// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until ALL sub-conditions are satisfied.
    /// Tracks per-condition completion state in the <see cref="MachineContext"/> blackboard
    /// so that multiple machines can safely share the same definition.
    /// </summary>
    public sealed class WaitForAllStep : IStep
    {
        private readonly CompositeCondition[] _conditions;
        private readonly string _contextPrefix;

        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>The sub-conditions that must all be satisfied.</summary>
        public CompositeCondition[] Conditions => _conditions;

        /// <summary>Creates a WaitForAllStep that blocks until every condition is met.</summary>
        public WaitForAllStep(CompositeCondition[] conditions, StepDebugInfo debugInfo)
        {
            if (conditions == null || conditions.Length == 0)
                throw new ArgumentException("WaitForAll requires at least one condition.", nameof(conditions));
            _conditions = conditions;
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));
            _contextPrefix = $"__wfa_{debugInfo.StateName}_{debugInfo.StepIndex}";
        }

        /// <inheritdoc />
        public StepResult Execute(MachineContext context)
        {
            bool allSatisfied = true;

            for (int i = 0; i < _conditions.Length; i++)
            {
                var cond = _conditions[i];

                switch (cond.Kind)
                {
                    case CompositeConditionKind.Event:
                        var eventKey = $"{_contextPrefix}_evt_{cond.EventId.Value}";
                        if (!context.Has(eventKey))
                        {
                            if (context.LastReceivedEvent == cond.EventId)
                                context.Set(eventKey, true);
                            else
                                allSatisfied = false;
                        }
                        break;

                    case CompositeConditionKind.Time:
                        var timeKey = $"{_contextPrefix}_time_{i}";
                        if (!context.TryGet<double>(timeKey, out var targetTime))
                        {
                            targetTime = context.CurrentTime + cond.Duration;
                            context.Set(timeKey, targetTime);
                        }
                        if (context.CurrentTime < targetTime)
                            allSatisfied = false;
                        break;

                    case CompositeConditionKind.Predicate:
                        if (!cond.Predicate(context))
                            allSatisfied = false;
                        break;
                }
            }

            if (allSatisfied)
            {
                Cleanup(context);
                return StepResult.Continue();
            }

            return StepResult.WaitForComposite();
        }

        private void Cleanup(MachineContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                var cond = _conditions[i];
                switch (cond.Kind)
                {
                    case CompositeConditionKind.Event:
                        context.Remove($"{_contextPrefix}_evt_{cond.EventId.Value}");
                        break;
                    case CompositeConditionKind.Time:
                        context.Remove($"{_contextPrefix}_time_{i}");
                        break;
                }
            }
        }
    }
}
