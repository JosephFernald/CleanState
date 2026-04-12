// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;

namespace CleanState.Debug
{
    /// <summary>
    /// Records a single state transition with full provenance.
    /// </summary>
    public sealed class TransitionTrace
    {
        public StateId FromState { get; }
        public StateId ToState { get; }
        public int TriggerStepIndex { get; }
        public TransitionReasonKind Reason { get; }
        public string Detail { get; }
        public float Timestamp { get; }

        public TransitionTrace(
            StateId fromState,
            StateId toState,
            int triggerStepIndex,
            TransitionReasonKind reason,
            string detail,
            float timestamp)
        {
            FromState = fromState;
            ToState = toState;
            TriggerStepIndex = triggerStepIndex;
            Reason = reason;
            Detail = detail;
            Timestamp = timestamp;
        }
    }
}