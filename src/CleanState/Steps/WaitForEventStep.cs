// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// Blocks until the specified event is received.
    /// On first execution, returns Block. When re-evaluated after event delivery, continues.
    /// </summary>
    public sealed class WaitForEventStep : IStep
    {
        public EventId EventId { get; }
        public StepDebugInfo DebugInfo { get; }

        public WaitForEventStep(EventId eventId, StepDebugInfo debugInfo)
        {
            EventId = eventId;
            DebugInfo = debugInfo;
        }

        public StepResult Execute(MachineContext context)
        {
            if (context.LastReceivedEvent == EventId)
                return StepResult.Continue();

            return StepResult.WaitForEvent(EventId);
        }
    }
}