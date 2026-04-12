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
        /// <summary>The event this step is waiting for.</summary>
        public EventId EventId { get; }
        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>Creates a new WaitForEventStep that blocks until the specified event is received.</summary>
        public WaitForEventStep(EventId eventId, StepDebugInfo debugInfo)
        {
            EventId = eventId;
            DebugInfo = debugInfo;
        }

        /// <inheritdoc />
        public StepResult Execute(MachineContext context)
        {
            if (context.LastReceivedEvent == EventId)
                return StepResult.Continue();

            return StepResult.WaitForEvent(EventId);
        }
    }
}