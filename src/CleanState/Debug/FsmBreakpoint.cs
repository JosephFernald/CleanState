// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;

namespace CleanState.Debug
{
    /// <summary>
    /// Describes a condition under which the machine should pause.
    /// Evaluated by FsmDebugController before each step.
    /// </summary>
    public sealed class FsmBreakpoint
    {
        /// <summary>The kind of condition this breakpoint checks.</summary>
        public FsmBreakpointKind Kind { get; }

        /// <summary>State to break on (for StateEntry/StateStep kinds).</summary>
        public StateId TargetState { get; }

        /// <summary>Step index to break on (for StateStep kind). -1 means any step.</summary>
        public int StepIndex { get; }

        /// <summary>Transition reason to break on (for TransitionReason kind).</summary>
        public TransitionReasonKind? TransitionReason { get; }

        /// <summary>Whether this breakpoint is currently active.</summary>
        public bool Enabled { get; set; }

        private FsmBreakpoint(FsmBreakpointKind kind, StateId targetState, int stepIndex, TransitionReasonKind? transitionReason)
        {
            Kind = kind;
            TargetState = targetState;
            StepIndex = stepIndex;
            TransitionReason = transitionReason;
            Enabled = true;
        }

        /// <summary>Break when entering the specified state (step 0).</summary>
        public static FsmBreakpoint OnStateEntry(StateId stateId)
        {
            return new FsmBreakpoint(FsmBreakpointKind.StateEntry, stateId, 0, null);
        }

        /// <summary>Break when reaching a specific step in a specific state.</summary>
        public static FsmBreakpoint OnStep(StateId stateId, int stepIndex)
        {
            return new FsmBreakpoint(FsmBreakpointKind.StateStep, stateId, stepIndex, null);
        }

        /// <summary>Break on any transition with the given reason kind.</summary>
        public static FsmBreakpoint OnTransitionReason(TransitionReasonKind reason)
        {
            return new FsmBreakpoint(FsmBreakpointKind.TransitionReason, StateId.Invalid, -1, reason);
        }

        /// <summary>
        /// Check if this breakpoint matches the current execution point.
        /// </summary>
        internal bool Matches(StateId currentState, int currentStep, StepDebugInfo stepInfo)
        {
            if (!Enabled) return false;

            switch (Kind)
            {
                case FsmBreakpointKind.StateEntry:
                    return currentState == TargetState && currentStep == 0;

                case FsmBreakpointKind.StateStep:
                    return currentState == TargetState && currentStep == StepIndex;

                case FsmBreakpointKind.TransitionReason:
                    // Transition reason breakpoints are checked after transitions,
                    // not before steps — handled separately by the controller.
                    return false;

                default:
                    return false;
            }
        }
    }

    /// <summary>Kinds of breakpoint conditions supported by the debug controller.</summary>
    public enum FsmBreakpointKind
    {
        /// <summary>Break when a specific state is entered.</summary>
        StateEntry,

        /// <summary>Break when a specific step index is reached in a state.</summary>
        StateStep,

        /// <summary>Break when a transition occurs for a given reason.</summary>
        TransitionReason
    }
}