// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Debug
{
    /// <summary>
    /// Point-in-time debug view of a machine's state.
    /// Detached copy — does not hold references to live machine internals.
    /// </summary>
    public sealed class DebugSnapshot
    {
        /// <summary>Name of the machine this snapshot was taken from.</summary>
        public string MachineName { get; }

        /// <summary>Machine status at the time of the snapshot.</summary>
        public MachineStatus Status { get; }

        /// <summary>Name of the state the machine was in.</summary>
        public string CurrentStateName { get; }

        /// <summary>Index of the step being executed within the current state.</summary>
        public int CurrentStepIndex { get; }

        /// <summary>Reason the machine is blocked, if any.</summary>
        public BlockKind BlockReason { get; }

        /// <summary>Last event the machine processed.</summary>
        public EventId LastEvent { get; }

        /// <summary>Last recorded transition trace.</summary>
        public TransitionTrace LastTransition { get; }

        /// <summary>Event the machine is waiting for, if blocked on an event.</summary>
        public EventId WaitingForEvent { get; }

        /// <summary>Display name of the event the machine is waiting for.</summary>
        public string WaitingForEventName { get; }

        /// <summary>Time the machine is waiting until, if blocked on a timer.</summary>
        public float WaitUntilTime { get; }

        /// <summary>Label of the current step.</summary>
        public string CurrentStepLabel { get; }

        /// <summary>Type name of the current step.</summary>
        public string CurrentStepType { get; }

        /// <summary>Total number of steps in the current state.</summary>
        public int StepCountInCurrentState { get; }

        /// <summary>Creates a new debug snapshot with the specified machine state.</summary>
        public DebugSnapshot(
            string machineName,
            MachineStatus status,
            string currentStateName,
            int currentStepIndex,
            BlockKind blockReason,
            EventId lastEvent,
            TransitionTrace lastTransition,
            EventId waitingForEvent,
            string waitingForEventName,
            float waitUntilTime,
            string currentStepLabel,
            string currentStepType,
            int stepCountInCurrentState)
        {
            MachineName = machineName;
            Status = status;
            CurrentStateName = currentStateName;
            CurrentStepIndex = currentStepIndex;
            BlockReason = blockReason;
            LastEvent = lastEvent;
            LastTransition = lastTransition;
            WaitingForEvent = waitingForEvent;
            WaitingForEventName = waitingForEventName;
            WaitUntilTime = waitUntilTime;
            CurrentStepLabel = currentStepLabel;
            CurrentStepType = currentStepType;
            StepCountInCurrentState = stepCountInCurrentState;
        }

        /// <summary>Returns a summary string of this snapshot.</summary>
        public override string ToString()
        {
            return $"[{MachineName}] {Status} — State: {CurrentStateName}, Step: {CurrentStepIndex}/{StepCountInCurrentState}, Block: {BlockReason}";
        }
    }
}