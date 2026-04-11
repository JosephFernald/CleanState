// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Builder
{
    /// <summary>
    /// Top-level fluent builder for defining a state machine.
    /// Compiles authored states into a flat MachineDefinition for runtime use.
    /// </summary>
    public sealed class MachineBuilder
    {
        private readonly string _machineName;
        private readonly List<StateBuilder> _stateBuilders = new List<StateBuilder>();
        private string _initialStateName;

        public MachineBuilder(string machineName)
        {
            _machineName = machineName ?? throw new ArgumentNullException(nameof(machineName));
        }

        /// <summary>
        /// Begin defining a new state. The first state defined becomes the initial state.
        /// </summary>
        public StateBuilder State(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("State name cannot be null or empty.", nameof(name));

            var builder = new StateBuilder(this, name);
            _stateBuilders.Add(builder);

            if (_initialStateName == null)
                _initialStateName = name;

            return builder;
        }

        /// <summary>
        /// Explicitly set which state the machine starts in.
        /// </summary>
        public MachineBuilder InitialState(string name)
        {
            _initialStateName = name;
            return this;
        }

        /// <summary>
        /// Compile all authored states into an immutable MachineDefinition.
        /// This is where string names are resolved to typed IDs.
        /// </summary>
        public MachineDefinition Build()
        {
            if (_stateBuilders.Count == 0)
                throw new InvalidOperationException("Machine must have at least one state.");

            var nameLookup = new NameLookup();

            // Phase 1: Assign StateIds and EventIds
            var stateNameToId = new Dictionary<string, StateId>();
            var eventNameToId = new Dictionary<string, EventId>();
            int nextStateId = 0;
            int nextEventId = 0;

            for (int i = 0; i < _stateBuilders.Count; i++)
            {
                var sb = _stateBuilders[i];
                var id = new StateId(nextStateId++);
                stateNameToId[sb.StateName] = id;
                nameLookup.RegisterState(id, sb.StateName);
            }

            // Collect all event names
            for (int i = 0; i < _stateBuilders.Count; i++)
            {
                var sb = _stateBuilders[i];
                for (int j = 0; j < sb.Steps.Count; j++)
                {
                    var step = sb.Steps[j];
                    if (step.Kind == StateBuilder.PendingStepKind.WaitForEvent && step.EventName != null)
                    {
                        if (!eventNameToId.ContainsKey(step.EventName))
                        {
                            var eid = new EventId(nextEventId++);
                            eventNameToId[step.EventName] = eid;
                            nameLookup.RegisterEvent(eid, step.EventName);
                        }
                    }
                }
            }

            // Phase 2: Compile states
            var stateDefinitions = new StateDefinition[_stateBuilders.Count];

            for (int i = 0; i < _stateBuilders.Count; i++)
            {
                var sb = _stateBuilders[i];
                var stateId = stateNameToId[sb.StateName];
                var steps = CompileSteps(sb, stateId, stateNameToId, eventNameToId, nameLookup);
                stateDefinitions[i] = new StateDefinition(stateId, sb.StateName, steps, sb.IsCheckpoint);
            }

            var initialStateId = stateNameToId[_initialStateName];

            return new MachineDefinition(_machineName, stateDefinitions, initialStateId, nameLookup);
        }

        /// <summary>
        /// Get the EventId for a named event. Useful for sending events to machines.
        /// Call after Build() — or use this helper to pre-register event names.
        /// </summary>
        public static EventId EventIdFrom(MachineDefinition definition, string eventName)
        {
            // Walk through states looking for matching event
            for (int i = 0; i < definition.StateCount; i++)
            {
                var state = definition.GetStateByIndex(i);
                for (int j = 0; j < state.Steps.Length; j++)
                {
                    if (state.Steps[j] is WaitForEventStep waitStep)
                    {
                        var name = definition.NameLookup.GetEventName(waitStep.EventId);
                        if (name == eventName)
                            return waitStep.EventId;
                    }
                }
            }
            throw new ArgumentException($"Event '{eventName}' not found in machine definition '{definition.Name}'.");
        }

        private IStep[] CompileSteps(
            StateBuilder sb,
            StateId stateId,
            Dictionary<string, StateId> stateNameToId,
            Dictionary<string, EventId> eventNameToId,
            NameLookup nameLookup)
        {
            int decisionIndex = 0;
            var compiled = new List<IStep>();

            for (int i = 0; i < sb.Steps.Count; i++)
            {
                var pending = sb.Steps[i];
                var debugInfo = new StepDebugInfo(
                    _machineName, sb.StateName, i, pending.Kind.ToString(),
                    pending.Label, pending.SourceFile, pending.SourceLine);

                switch (pending.Kind)
                {
                    case StateBuilder.PendingStepKind.Action:
                        compiled.Add(new ActionStep(pending.Action, debugInfo));
                        break;

                    case StateBuilder.PendingStepKind.WaitForEvent:
                        var eventId = eventNameToId[pending.EventName];
                        compiled.Add(new WaitForEventStep(eventId, debugInfo));
                        break;

                    case StateBuilder.PendingStepKind.WaitForTime:
                        compiled.Add(new WaitForTimeStep(pending.Duration, debugInfo));
                        break;

                    case StateBuilder.PendingStepKind.WaitForPredicate:
                        compiled.Add(new WaitForPredicateStep(pending.Predicate, debugInfo));
                        break;

                    case StateBuilder.PendingStepKind.GoTo:
                        if (!stateNameToId.TryGetValue(pending.TargetState, out var targetId))
                            throw new InvalidOperationException(
                                $"GoTo target state '{pending.TargetState}' not found in machine '{_machineName}'.");
                        compiled.Add(new TransitionStep(targetId, debugInfo));
                        break;

                    case StateBuilder.PendingStepKind.DecisionPlaceholder:
                        if (decisionIndex >= sb.Decisions.Count)
                            throw new InvalidOperationException(
                                $"Decision placeholder without matching Decision/Otherwise in state '{sb.StateName}'.");

                        var decision = sb.Decisions[decisionIndex++];
                        var branches = new DecisionBranch[decision.Branches.Count];
                        for (int b = 0; b < decision.Branches.Count; b++)
                        {
                            var pb = decision.Branches[b];
                            if (!stateNameToId.TryGetValue(pb.TargetStateName, out var branchTarget))
                                throw new InvalidOperationException(
                                    $"Decision branch target '{pb.TargetStateName}' not found in machine '{_machineName}'.");
                            branches[b] = new DecisionBranch(pb.Condition, branchTarget, pb.Label);
                        }
                        compiled.Add(new DecisionStep(branches, debugInfo));
                        break;
                }
            }

            return compiled.ToArray();
        }
    }
}