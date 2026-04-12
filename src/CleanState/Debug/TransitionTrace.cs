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
        /// <summary>State the machine transitioned from.</summary>
        public StateId FromState { get; }

        /// <summary>State the machine transitioned to.</summary>
        public StateId ToState { get; }

        /// <summary>Index of the step that triggered the transition.</summary>
        public int TriggerStepIndex { get; }

        /// <summary>Why the transition occurred.</summary>
        public TransitionReasonKind Reason { get; }

        /// <summary>Optional human-readable detail about the transition.</summary>
        public string Detail { get; }

        /// <summary>Time at which the transition occurred.</summary>
        public float Timestamp { get; }

        /// <summary>Creates a transition trace with full provenance information.</summary>
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