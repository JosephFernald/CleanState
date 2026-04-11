// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until the specified duration has elapsed from when the step was first reached.
    /// Stateless — stores the target time in MachineContext so multiple machines can
    /// safely share the same compiled MachineDefinition.
    /// </summary>
    public sealed class WaitForTimeStep : IStep
    {
        public float Duration { get; }
        public StepDebugInfo DebugInfo { get; }

        private readonly string _contextKey;

        public WaitForTimeStep(float duration, StepDebugInfo debugInfo)
        {
            Duration = duration;
            DebugInfo = debugInfo ?? throw new System.ArgumentNullException(nameof(debugInfo));
            _contextKey = $"__wft_{debugInfo.StateName}_{debugInfo.StepIndex}";
        }

        public StepResult Execute(MachineContext context)
        {
            if (!context.TryGet<float>(_contextKey, out var targetTime))
            {
                targetTime = context.CurrentTime + Duration;
                context.Set(_contextKey, targetTime);
            }

            if (context.CurrentTime >= targetTime)
            {
                context.Remove(_contextKey);
                return StepResult.Continue();
            }

            return StepResult.WaitForTime(targetTime);
        }
    }
}