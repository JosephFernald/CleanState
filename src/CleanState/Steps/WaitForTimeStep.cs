// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until the specified duration has elapsed from when the step was first reached.
    /// </summary>
    public sealed class WaitForTimeStep : IStep
    {
        public float Duration { get; }
        public StepDebugInfo DebugInfo { get; }

        private float _targetTime;
        private bool _initialized;

        public WaitForTimeStep(float duration, StepDebugInfo debugInfo)
        {
            Duration = duration;
            DebugInfo = debugInfo;
        }

        public StepResult Execute(MachineContext context)
        {
            if (!_initialized)
            {
                _targetTime = context.CurrentTime + Duration;
                _initialized = true;
            }

            if (context.CurrentTime >= _targetTime)
            {
                _initialized = false; // reset for potential re-entry
                return StepResult.Continue();
            }

            return StepResult.WaitForTime(_targetTime);
        }
    }
}