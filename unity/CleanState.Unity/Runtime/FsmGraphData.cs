// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace CleanState.Unity.Runtime
{
    /// <summary>
    /// Lightweight, engine-agnostic snapshot of a machine's graph topology.
    /// Produced by extension methods on MachineDefinition — consumed by any visualizer.
    /// No Unity types. Disposable projection, not source of truth.
    /// </summary>
    public sealed class FsmGraphData
    {
        public string MachineName;
        public List<NodeData> Nodes = new List<NodeData>();
        public List<EdgeData> Edges = new List<EdgeData>();
    }

    public sealed class NodeData
    {
        public int StateIdValue;
        public string Name;
        public bool IsInitial;
        public bool IsCheckpoint;
        public List<StepData> Steps = new List<StepData>();
    }

    public sealed class StepData
    {
        public string Label;
        public string StepType;
    }

    public sealed class EdgeData
    {
        public int FromStateIdValue;
        public int ToStateIdValue;
        public string Label;
        public EdgeKind Kind;
    }

    public enum EdgeKind
    {
        Direct,
        DecisionBranch
    }

    /// <summary>
    /// Lightweight, engine-agnostic snapshot of a machine's live execution state.
    /// Polled each frame by the visualizer. Detached copy, not a live reference.
    /// </summary>
    public sealed class FsmLiveState
    {
        public string MachineName;
        public string Status;
        public int ActiveStateIdValue = -1;
        public string ActiveStateName;
        public int ActiveStepIndex;
        public int StepCountInActiveState;
        public string BlockReason;

        // Enriched block detail
        public string WaitingForEventName;
        public float WaitUntilTime;

        // Current step detail
        public string CurrentStepLabel;
        public string CurrentStepType;

        // Last event
        public string LastEventName;

        // Last transition detail
        public string LastTransitionDetail;
        public string LastTransitionReason;
        public float LastTransitionTimestamp;
        public int LastTransitionFromStateId = -1;
        public int LastTransitionToStateId = -1;

        // Transition history
        public List<TransitionRecord> RecentTransitions = new List<TransitionRecord>();
    }

    public sealed class TransitionRecord
    {
        public int FromStateIdValue;
        public int ToStateIdValue;
        public string Detail;
        public string Reason;
        public float Timestamp;
    }
}