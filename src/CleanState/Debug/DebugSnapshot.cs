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
        public string MachineName { get; }
        public MachineStatus Status { get; }
        public string CurrentStateName { get; }
        public int CurrentStepIndex { get; }
        public BlockKind BlockReason { get; }
        public EventId LastEvent { get; }
        public TransitionTrace LastTransition { get; }

        // Enriched blocking info
        public EventId WaitingForEvent { get; }
        public string WaitingForEventName { get; }
        public float WaitUntilTime { get; }

        // Current step detail
        public string CurrentStepLabel { get; }
        public string CurrentStepType { get; }

        // Total steps in current state (for progress display)
        public int StepCountInCurrentState { get; }

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

        public override string ToString()
        {
            return $"[{MachineName}] {Status} — State: {CurrentStateName}, Step: {CurrentStepIndex}/{StepCountInCurrentState}, Block: {BlockReason}";
        }
    }
}