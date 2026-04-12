// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Identity;
using CleanState.Runtime;

namespace CleanState.Debug
{
    /// <summary>
    /// Explicit, opt-in debug command interface for a machine.
    /// This is the ONLY sanctioned way for tooling to issue debug commands.
    ///
    /// The owner of the Machine decides whether to create one of these.
    /// The editor never gets a raw Machine reference — it gets IFsmObservable for
    /// reading and optionally an FsmDebugController for debug commands.
    ///
    /// Supports:
    ///   - Pause / Resume
    ///   - Step once (while paused)
    ///   - Jump to state (while paused)
    ///   - Breakpoints (state entry, step index, transition reason)
    /// </summary>
    public sealed class FsmDebugController
    {
        private readonly Machine _machine;
        private readonly List<FsmBreakpoint> _breakpoints = new List<FsmBreakpoint>();
        private DebugCommand _pendingCommand;
        private bool _paused;
        private bool _breakpointHit;
        private FsmBreakpoint _lastHitBreakpoint;

        public bool IsPaused => _paused;
        public bool HasPendingCommand => _pendingCommand.Kind != DebugCommandKind.None;
        public bool BreakpointHit => _breakpointHit;
        public FsmBreakpoint LastHitBreakpoint => _lastHitBreakpoint;
        public int BreakpointCount => _breakpoints.Count;

        public FsmDebugController(Machine machine)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));

            // Wire into the machine's BeforeStep hook
            _machine.BeforeStep = OnBeforeStep;

            // Listen for transitions to check transition-reason breakpoints
            _machine.OnTransition += OnTransition;
        }

        // --- Pause / Resume ---

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
            _breakpointHit = false;
            _lastHitBreakpoint = null;
        }

        // --- Step / Jump (require paused) ---

        public void StepOnce()
        {
            if (!_paused)
                throw new InvalidOperationException("StepOnce requires the machine to be paused.");

            _pendingCommand = new DebugCommand(DebugCommandKind.StepOnce);
        }

        public void JumpToState(StateId stateId)
        {
            if (!_paused)
                throw new InvalidOperationException("JumpToState requires the machine to be paused.");

            _pendingCommand = new DebugCommand(DebugCommandKind.JumpToState, stateId);
        }

        // --- Breakpoints ---

        public FsmBreakpoint AddBreakpoint(FsmBreakpoint breakpoint)
        {
            if (breakpoint == null) throw new ArgumentNullException(nameof(breakpoint));
            _breakpoints.Add(breakpoint);
            return breakpoint;
        }

        public bool RemoveBreakpoint(FsmBreakpoint breakpoint)
        {
            return _breakpoints.Remove(breakpoint);
        }

        public void ClearBreakpoints()
        {
            _breakpoints.Clear();
        }

        public FsmBreakpoint GetBreakpoint(int index) => _breakpoints[index];

        // --- Scheduler integration ---

        public bool ShouldExecute()
        {
            if (!_paused)
                return true;

            return _pendingCommand.Kind != DebugCommandKind.None;
        }

        public bool ApplyPendingCommand(float currentTime)
        {
            if (_pendingCommand.Kind == DebugCommandKind.None)
                return false;

            var cmd = _pendingCommand;
            _pendingCommand = default;

            switch (cmd.Kind)
            {
                case DebugCommandKind.StepOnce:
                    // Temporarily remove the hook so one step can execute,
                    // then re-pause after
                    _machine.BeforeStep = null;
                    _machine.Update(currentTime);
                    _machine.BeforeStep = OnBeforeStep;
                    _paused = true;
                    return true;

                case DebugCommandKind.JumpToState:
                    _machine.ForceState(cmd.TargetState, currentTime);
                    _paused = true;
                    return true;
            }

            return false;
        }

        // --- Internal hooks ---

        private bool OnBeforeStep(StateId stateId, int stepIndex, StepDebugInfo debugInfo)
        {
            // If paused (not from a breakpoint), always break
            if (_paused)
                return true;

            // Check breakpoints
            for (int i = 0; i < _breakpoints.Count; i++)
            {
                if (_breakpoints[i].Matches(stateId, stepIndex, debugInfo))
                {
                    _paused = true;
                    _breakpointHit = true;
                    _lastHitBreakpoint = _breakpoints[i];
                    return true;
                }
            }

            return false;
        }

        private void OnTransition(TransitionTrace trace)
        {
            // Check transition-reason breakpoints
            for (int i = 0; i < _breakpoints.Count; i++)
            {
                var bp = _breakpoints[i];
                if (bp.Enabled &&
                    bp.Kind == FsmBreakpointKind.TransitionReason &&
                    bp.TransitionReason.HasValue &&
                    bp.TransitionReason.Value == trace.Reason)
                {
                    _paused = true;
                    _breakpointHit = true;
                    _lastHitBreakpoint = bp;
                    break;
                }
            }
        }

        // --- Internal types ---

        private enum DebugCommandKind
        {
            None,
            StepOnce,
            JumpToState
        }

        private struct DebugCommand
        {
            public DebugCommandKind Kind;
            public StateId TargetState;

            public DebugCommand(DebugCommandKind kind, StateId targetState = default)
            {
                Kind = kind;
                TargetState = targetState;
            }
        }
    }
}