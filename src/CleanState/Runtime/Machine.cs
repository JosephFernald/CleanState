// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Steps;

namespace CleanState.Runtime
{
    /// <summary>
    /// Runtime instance of a state machine. Executes steps from a compiled MachineDefinition
    /// using the run-until-blocked model.
    /// </summary>
    public sealed class Machine : IFsmObservable
    {
        private readonly MachineDefinition _definition;
        private readonly MachineContext _context;
        private readonly TraceBuffer _traceBuffer;

        private StateDefinition _currentStateDef;
        private int _currentStepIndex;
        private BlockKind _blockKind;
        private EventId _waitingForEvent;
        private float _waitUntilTime;
        private TransitionTrace _lastTransition;

        /// <summary>Unique identifier for this machine instance.</summary>
        public MachineId Id { get; }
        /// <summary>Current execution status of the machine.</summary>
        public MachineStatus Status { get; private set; }
        /// <summary>The compiled definition this machine executes.</summary>
        public MachineDefinition Definition => _definition;
        /// <summary>Shared key-value context available to all steps.</summary>
        public MachineContext Context => _context;
        /// <summary>The state currently being executed, or <see cref="StateId.Invalid"/> if none.</summary>
        public StateId CurrentState => _currentStateDef != null ? _currentStateDef.Id : StateId.Invalid;
        /// <summary>Index of the current step within the active state.</summary>
        public int CurrentStepIndex => _currentStepIndex;
        /// <summary>The kind of block keeping the machine from progressing, if any.</summary>
        public BlockKind BlockReason => _blockKind;

        /// <summary>
        /// Raised when the machine transitions between states.
        /// </summary>
        public event Action<TransitionTrace> OnTransition;

        /// <summary>
        /// Raised when the machine completes (runs out of steps in a state with no transition).
        /// </summary>
        public event Action<Machine> OnCompleted;

        /// <summary>
        /// Optional hook checked before each step. If set and returns true,
        /// the machine pauses (returns to Blocked with BlockKind.None).
        /// Used by FsmDebugController for breakpoints. Not part of IFsmObservable.
        /// </summary>
        public Func<StateId, int, StepDebugInfo, bool> BeforeStep;

        /// <summary>Creates a new machine instance from the given definition.</summary>
        public Machine(MachineId id, MachineDefinition definition, TraceBuffer traceBuffer = null)
        {
            Id = id;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _context = new MachineContext();
            _traceBuffer = traceBuffer;
            Status = MachineStatus.Idle;
        }

        /// <summary>
        /// Start the machine at its initial state.
        /// </summary>
        public void Start(float currentTime)
        {
            if (Status != MachineStatus.Idle)
                throw new InvalidOperationException($"Cannot start machine '{_definition.Name}' — status is {Status}.");

            _context.CurrentTime = currentTime;
            EnterState(_definition.InitialState, currentTime);
            Status = MachineStatus.Running;
            RunUntilBlocked(currentTime);
        }

        /// <summary>
        /// Send an event to this machine. If it's waiting for this event, it will unblock and continue.
        /// </summary>
        public void SendEvent(EventId eventId, float currentTime)
        {
            if (Status == MachineStatus.Completed || Status == MachineStatus.Faulted)
                return;

            if (Status == MachineStatus.Blocked && _blockKind == BlockKind.WaitForEvent && _waitingForEvent == eventId)
            {
                _context.LastReceivedEvent = eventId;
                _context.CurrentTime = currentTime;
                Status = MachineStatus.Running;
                RunUntilBlocked(currentTime);
            }
        }

        /// <summary>
        /// Tick the machine. Checks time-based and predicate-based blocks.
        /// Called by the scheduler each frame.
        /// </summary>
        public void Update(float currentTime)
        {
            if (Status != MachineStatus.Blocked)
                return;

            _context.CurrentTime = currentTime;

            bool canResume = false;

            switch (_blockKind)
            {
                case BlockKind.WaitForTime:
                    canResume = currentTime >= _waitUntilTime;
                    break;
                case BlockKind.WaitForPredicate:
                    canResume = true; // re-evaluate the step
                    break;
                default:
                    return; // event-blocked machines only resume via SendEvent
            }

            if (canResume)
            {
                Status = MachineStatus.Running;
                RunUntilBlocked(currentTime);
            }
        }

        /// <summary>
        /// Force the machine into a specific state. Used for recovery and external commands.
        /// </summary>
        public void ForceState(StateId stateId, float currentTime, int stepIndex = 0)
        {
            _context.CurrentTime = currentTime;
            EnterState(stateId, currentTime, stepIndex);
            Status = MachineStatus.Running;
            RunUntilBlocked(currentTime);
        }

        /// <summary>Captures the current machine state as an immutable debug snapshot.</summary>
        public DebugSnapshot GetDebugSnapshot()
        {
            // Resolve current step info
            string stepLabel = null;
            string stepType = null;
            int stepCount = 0;

            if (_currentStateDef != null)
            {
                stepCount = _currentStateDef.Steps.Length;
                if (_currentStepIndex < _currentStateDef.Steps.Length)
                {
                    var step = _currentStateDef.Steps[_currentStepIndex];
                    stepLabel = step.DebugInfo.Label;
                    stepType = step.DebugInfo.StepType;
                }
            }

            // Resolve waiting event name from lookup
            string waitEventName = null;
            if (_waitingForEvent.IsValid)
                waitEventName = _definition.NameLookup.GetEventName(_waitingForEvent);

            return new DebugSnapshot(
                _definition.Name,
                Status,
                _currentStateDef != null ? _currentStateDef.Name : "(none)",
                _currentStepIndex,
                _blockKind,
                _context.LastReceivedEvent,
                _lastTransition,
                _waitingForEvent,
                waitEventName,
                _waitUntilTime,
                stepLabel,
                stepType,
                stepCount);
        }

        private void RunUntilBlocked(float currentTime)
        {
            int safetyLimit = 10000;

            while (Status == MachineStatus.Running && safetyLimit-- > 0)
            {
                if (_currentStateDef == null || _currentStepIndex >= _currentStateDef.Steps.Length)
                {
                    // Reached end of state with no transition — machine is complete
                    Status = MachineStatus.Completed;
                    OnCompleted?.Invoke(this);
                    return;
                }

                var step = _currentStateDef.Steps[_currentStepIndex];

                // Check breakpoint hook before executing
                if (BeforeStep != null && BeforeStep(_currentStateDef.Id, _currentStepIndex, step.DebugInfo))
                {
                    Status = MachineStatus.Blocked;
                    _blockKind = BlockKind.None;
                    return;
                }

                StepResult result;

                try
                {
                    result = step.Execute(_context);
                }
                catch (Exception ex)
                {
                    Status = MachineStatus.Faulted;
                    throw new FsmExecutionException(step.DebugInfo, ex);
                }

                switch (result.Kind)
                {
                    case StepResultKind.Continue:
                        _context.LastReceivedEvent = EventId.Invalid;
                        _currentStepIndex++;
                        break;

                    case StepResultKind.Block:
                        Status = MachineStatus.Blocked;
                        _blockKind = result.BlockKind;
                        _waitingForEvent = result.WaitEventId;
                        _waitUntilTime = result.WaitUntilTime;
                        return;

                    case StepResultKind.TransitionTo:
                        var fromState = _currentStateDef.Id;
                        var reason = step is DecisionStep
                            ? TransitionReasonKind.DecisionBranch
                            : TransitionReasonKind.Direct;

                        RecordTransition(fromState, result.TargetState, _currentStepIndex, reason, step.DebugInfo.Label, currentTime);
                        EnterState(result.TargetState, currentTime);
                        break;
                }
            }

            if (safetyLimit <= 0)
            {
                Status = MachineStatus.Faulted;
                throw new InvalidOperationException(
                    $"Machine '{_definition.Name}' exceeded step execution limit. Possible infinite loop.");
            }
        }

        private void EnterState(StateId stateId, float currentTime, int stepIndex = 0)
        {
            _currentStateDef = _definition.GetState(stateId);
            _currentStepIndex = stepIndex;
            _context.CurrentState = stateId;
            _blockKind = BlockKind.None;
            _waitingForEvent = EventId.Invalid;
            _waitUntilTime = 0f;
        }

        private void RecordTransition(StateId from, StateId to, int stepIndex, TransitionReasonKind reason, string detail, float timestamp)
        {
            var trace = new TransitionTrace(from, to, stepIndex, reason, detail, timestamp);
            _lastTransition = trace;
            _traceBuffer?.Record(trace);
            OnTransition?.Invoke(trace);
        }
    }
}