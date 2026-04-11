// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Unity.Runtime
{
    /// <summary>
    /// Extension methods that extract visualization-friendly data from core FSM types.
    /// These operate on IFsmObservable (read-only) and MachineDefinition (immutable).
    /// No mutation. No context access. Observation only.
    /// </summary>
    public static class MachineExtensions
    {
        /// <summary>
        /// Extract the full graph topology from a compiled MachineDefinition.
        /// Call once at build time or when the definition changes — not per frame.
        /// </summary>
        public static FsmGraphData ExtractGraphData(this MachineDefinition definition)
        {
            var graph = new FsmGraphData { MachineName = definition.Name };

            for (int i = 0; i < definition.StateCount; i++)
            {
                var state = definition.GetStateByIndex(i);
                var node = new NodeData
                {
                    StateIdValue = state.Id.Value,
                    Name = state.Name,
                    IsInitial = state.Id == definition.InitialState,
                    IsCheckpoint = state.IsCheckpoint
                };

                for (int s = 0; s < state.Steps.Length; s++)
                {
                    node.Steps.Add(new StepData
                    {
                        Label = state.Steps[s].DebugInfo.Label,
                        StepType = state.Steps[s].DebugInfo.StepType
                    });
                }

                graph.Nodes.Add(node);
            }

            for (int i = 0; i < definition.StateCount; i++)
            {
                var state = definition.GetStateByIndex(i);

                for (int s = 0; s < state.Steps.Length; s++)
                {
                    var step = state.Steps[s];

                    if (step is TransitionStep transition)
                    {
                        graph.Edges.Add(new EdgeData
                        {
                            FromStateIdValue = state.Id.Value,
                            ToStateIdValue = transition.TargetState.Value,
                            Label = step.DebugInfo.Label,
                            Kind = EdgeKind.Direct
                        });
                    }
                    else if (step is DecisionStep decision)
                    {
                        var branches = decision.GetBranches();
                        if (branches != null)
                        {
                            for (int b = 0; b < branches.Length; b++)
                            {
                                graph.Edges.Add(new EdgeData
                                {
                                    FromStateIdValue = state.Id.Value,
                                    ToStateIdValue = branches[b].TargetState.Value,
                                    Label = branches[b].Label,
                                    Kind = EdgeKind.DecisionBranch
                                });
                            }
                        }
                    }
                }
            }

            return graph;
        }

        /// <summary>
        /// Extract the current live execution state from an observable machine.
        /// Read-only. Safe to call every frame.
        /// </summary>
        public static FsmLiveState ExtractLiveState(this IFsmObservable observable)
        {
            var snapshot = observable.GetDebugSnapshot();
            var lookup = observable.Definition.NameLookup;

            var live = new FsmLiveState
            {
                MachineName = snapshot.MachineName,
                Status = snapshot.Status.ToString(),
                ActiveStateIdValue = observable.CurrentState.Value,
                ActiveStateName = snapshot.CurrentStateName,
                ActiveStepIndex = snapshot.CurrentStepIndex,
                StepCountInActiveState = snapshot.StepCountInCurrentState,
                BlockReason = snapshot.BlockReason.ToString(),
                WaitingForEventName = snapshot.WaitingForEventName,
                WaitUntilTime = snapshot.WaitUntilTime,
                CurrentStepLabel = snapshot.CurrentStepLabel,
                CurrentStepType = snapshot.CurrentStepType
            };

            if (snapshot.LastEvent.IsValid)
                live.LastEventName = lookup.GetEventName(snapshot.LastEvent);

            if (snapshot.LastTransition != null)
            {
                live.LastTransitionDetail = snapshot.LastTransition.Detail;
                live.LastTransitionReason = snapshot.LastTransition.Reason.ToString();
                live.LastTransitionTimestamp = snapshot.LastTransition.Timestamp;
                live.LastTransitionFromStateId = snapshot.LastTransition.FromState.Value;
                live.LastTransitionToStateId = snapshot.LastTransition.ToState.Value;
            }

            return live;
        }

        /// <summary>
        /// Extract live state and include recent transitions from a trace buffer.
        /// </summary>
        public static FsmLiveState ExtractLiveState(this IFsmObservable observable, TraceBuffer traceBuffer)
        {
            var live = observable.ExtractLiveState();

            if (traceBuffer != null)
            {
                var traces = traceBuffer.GetTraces();
                int start = traces.Length > 16 ? traces.Length - 16 : 0;
                for (int i = start; i < traces.Length; i++)
                {
                    live.RecentTransitions.Add(new TransitionRecord
                    {
                        FromStateIdValue = traces[i].FromState.Value,
                        ToStateIdValue = traces[i].ToState.Value,
                        Detail = traces[i].Detail,
                        Reason = traces[i].Reason.ToString(),
                        Timestamp = traces[i].Timestamp
                    });
                }
            }

            return live;
        }
    }
}