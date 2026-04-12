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

        /// <summary>Whether the machine is currently paused.</summary>
        public bool IsPaused => _paused;

        /// <summary>Whether a debug command is queued for execution.</summary>
        public bool HasPendingCommand => _pendingCommand.Kind != DebugCommandKind.None;

        /// <summary>Whether the pause was caused by hitting a breakpoint.</summary>
        public bool BreakpointHit => _breakpointHit;

        /// <summary>The breakpoint that caused the most recent pause, if any.</summary>
        public FsmBreakpoint LastHitBreakpoint => _lastHitBreakpoint;

        /// <summary>Number of registered breakpoints.</summary>
        public int BreakpointCount => _breakpoints.Count;

        /// <summary>Creates a debug controller and wires it into the specified machine.</summary>
        public FsmDebugController(Machine machine)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));

            // Wire into the machine's BeforeStep hook
            _machine.BeforeStep = OnBeforeStep;

            // Listen for transitions to check transition-reason breakpoints
            _machine.OnTransition += OnTransition;
        }

        // --- Pause / Resume ---

        /// <summary>Pauses the machine before the next step.</summary>
        public void Pause()
        {
            _paused = true;
        }

        /// <summary>Resumes normal execution and clears breakpoint-hit state.</summary>
        public void Resume()
        {
            _paused = false;
            _breakpointHit = false;
            _lastHitBreakpoint = null;
        }

        // --- Step / Jump (require paused) ---

        /// <summary>Queues a single-step command. Machine must be paused.</summary>
        public void StepOnce()
        {
            if (!_paused)
                throw new InvalidOperationException("StepOnce requires the machine to be paused.");

            _pendingCommand = new DebugCommand(DebugCommandKind.StepOnce);
        }

        /// <summary>Queues a jump to the specified state. Machine must be paused.</summary>
        public void JumpToState(StateId stateId)
        {
            if (!_paused)
                throw new InvalidOperationException("JumpToState requires the machine to be paused.");

            _pendingCommand = new DebugCommand(DebugCommandKind.JumpToState, stateId);
        }

        // --- Breakpoints ---

        /// <summary>Registers a breakpoint and returns it.</summary>
        public FsmBreakpoint AddBreakpoint(FsmBreakpoint breakpoint)
        {
            if (breakpoint == null) throw new ArgumentNullException(nameof(breakpoint));
            _breakpoints.Add(breakpoint);
            return breakpoint;
        }

        /// <summary>Removes the specified breakpoint. Returns true if found.</summary>
        public bool RemoveBreakpoint(FsmBreakpoint breakpoint)
        {
            return _breakpoints.Remove(breakpoint);
        }

        /// <summary>Removes all registered breakpoints.</summary>
        public void ClearBreakpoints()
        {
            _breakpoints.Clear();
        }

        /// <summary>Returns the breakpoint at the specified index.</summary>
        public FsmBreakpoint GetBreakpoint(int index) => _breakpoints[index];

        // --- Scheduler integration ---

        /// <summary>Returns true if the machine should execute its next update.</summary>
        public bool ShouldExecute()
        {
            if (!_paused)
                return true;

            return _pendingCommand.Kind != DebugCommandKind.None;
        }

        /// <summary>Executes the pending debug command, if any. Returns true if a command was applied.</summary>
        public bool ApplyPendingCommand(double currentTime)
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