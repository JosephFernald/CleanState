// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>Indicates what the machine should do after a step executes.</summary>
    public enum StepResultKind
    {
        /// <summary>Advance to the next step in the current state.</summary>
        Continue,
        /// <summary>Pause execution until a blocking condition is resolved.</summary>
        Block,
        /// <summary>Transition to a different state immediately.</summary>
        TransitionTo
    }

    /// <summary>Describes the type of blocking condition when a step returns Block.</summary>
    public enum BlockKind
    {
        /// <summary>No blocking; used when the result is not a Block.</summary>
        None,
        /// <summary>Waiting for a specific event to be received.</summary>
        WaitForEvent,
        /// <summary>Waiting until a point in time is reached.</summary>
        WaitForTime,
        /// <summary>Waiting until a predicate evaluates to true.</summary>
        WaitForPredicate,
        /// <summary>Waiting for a child machine to complete.</summary>
        WaitForChildMachine
    }

    /// <summary>
    /// Return value from step execution. Tells the machine what to do next.
    /// Struct to avoid allocation.
    /// </summary>
    public readonly struct StepResult
    {
        /// <summary>The action the machine should take after this step.</summary>
        public readonly StepResultKind Kind;
        /// <summary>The destination state when Kind is TransitionTo.</summary>
        public readonly StateId TargetState;
        /// <summary>The type of blocking condition when Kind is Block.</summary>
        public readonly BlockKind BlockKind;
        /// <summary>The event to wait for when blocking on an event.</summary>
        public readonly EventId WaitEventId;
        /// <summary>The target time to wait until when blocking on time.</summary>
        public readonly float WaitUntilTime;

        private StepResult(StepResultKind kind, StateId targetState, BlockKind blockKind, EventId waitEventId, float waitUntilTime)
        {
            Kind = kind;
            TargetState = targetState;
            BlockKind = blockKind;
            WaitEventId = waitEventId;
            WaitUntilTime = waitUntilTime;
        }

        /// <summary>Creates a result that advances to the next step.</summary>
        public static StepResult Continue() => new StepResult(StepResultKind.Continue, StateId.Invalid, BlockKind.None, EventId.Invalid, 0f);

        /// <summary>Creates a result that transitions to the specified state.</summary>
        public static StepResult Transition(StateId targetState) => new StepResult(StepResultKind.TransitionTo, targetState, BlockKind.None, EventId.Invalid, 0f);

        /// <summary>Creates a result that blocks until the specified event is received.</summary>
        public static StepResult WaitForEvent(EventId eventId) => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForEvent, eventId, 0f);

        /// <summary>Creates a result that blocks until the specified time is reached.</summary>
        public static StepResult WaitForTime(float untilTime) => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForTime, EventId.Invalid, untilTime);

        /// <summary>Creates a result that blocks until a predicate becomes true.</summary>
        public static StepResult WaitForPredicate() => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForPredicate, EventId.Invalid, 0f);

        /// <summary>Creates a result that blocks until a child machine completes.</summary>
        public static StepResult WaitForChild() => new StepResult(StepResultKind.Block, StateId.Invalid, BlockKind.WaitForChildMachine, EventId.Invalid, 0f);
    }
}