// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;

namespace CleanState.Steps
{
    public enum StepResultKind
    {
        Continue,
        Block,
        TransitionTo
    }

    public enum BlockKind
    {
        None,
        WaitForEvent,
        WaitForTime,
        WaitForPredicate,
        WaitForChildMachine
    }

    /// <summary>
    /// Return value from step execution. Tells the machine what to do next.
    /// Struct to avoid allocation.
    /// </summary>
    public readonly struct StepResult
    {
        public readonly StepResultKind Kind;
        public readonly StateId TargetState;
        public readonly BlockKind BlockKind;
        public readonly EventId WaitEventId;
        public readonly float WaitUntilTime;

        private StepResult(StepResultKind kind, StateId targetState, BlockKind blockKind, EventId waitEventId, float waitUntilTime)
        {
            Kind = kind;
            TargetState = targetState;
            BlockKind = blockKind;
            WaitEventId = waitEventId;
            WaitUntilTime = waitUntilTime;
        }

        public static StepResult Continue() => new StepResult(StepResultKind.Continue, StateId.Invalid, BlockKind.None, EventId.Invalid, 0f);

        public static StepResult Transition(StateId targetState) => new StepResult(StepResultKind.TransitionTo, targetState, BlockKind.None, EventId.Invalid, 0f);

        public static StepResult WaitForEvent(EventId eventId) => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForEvent, eventId, 0f);

        public static StepResult WaitForTime(float untilTime) => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForTime, EventId.Invalid, untilTime);

        public static StepResult WaitForPredicate() => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForPredicate, EventId.Invalid, 0f);

        public static StepResult WaitForChild() => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForChildMachine, EventId.Invalid, 0f);
    }
}