// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CleanState.Unity.Runtime;

namespace CleanState.Unity.Editor
{
    /// <summary>
    /// GraphView that displays an FSM's topology and live execution state.
    ///
    /// SOURCE OF TRUTH CONTRACT:
    ///   The graph is a disposable projection of:
    ///     1. MachineDefinition (topology — nodes and edges)
    ///     2. IFsmObservable    (live state — active node, step, block reason)
    ///
    ///   This view is NEVER the source of truth. It does not:
    ///     - Persist graph layout across sessions
    ///     - Feed node positions back into the FSM
    ///     - Cache state that outlives the current MachineDefinition
    ///     - Allow structural edits (no adding/removing nodes or edges)
    ///
    ///   Rebuild from definition whenever it might have changed.
    ///   Re-derive visual state from IFsmObservable every frame.
    /// </summary>
    public sealed class FsmGraphView : GraphView
    {
        private readonly Dictionary<int, FsmStateNode> _nodesByStateId = new Dictionary<int, FsmStateNode>();
        private readonly List<Edge> _edges = new List<Edge>();

        /// <summary>
        /// Raised when a user toggles a breakpoint on a state node.
        /// Provides the state ID value and whether the breakpoint is now active.
        /// </summary>
        public System.Action<int, bool> OnBreakpointToggled;

        /// <summary>
        /// Cached topology used only for edge-index lookups during live state updates.
        /// This is a read-only snapshot from ExtractGraphData — not editor state.
        /// Nulled on ClearGraph. Regenerated on every BuildFromGraphData call.
        /// </summary>
        private FsmGraphData _cachedTopology;

        public FsmGraphView()
        {
            // Navigation manipulators (zoom, pan) — observation only
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            style.flexGrow = 1;
        }

        /// <summary>
        /// Build the graph from extracted topology data.
        /// Destroys all existing visual elements and rebuilds from scratch.
        /// The graphData is a projection of MachineDefinition — we do not own it.
        /// </summary>
        public void BuildFromGraphData(FsmGraphData graphData)
        {
            ClearGraph();

            if (graphData == null) return;

            _cachedTopology = graphData;

            // Create nodes
            for (int i = 0; i < graphData.Nodes.Count; i++)
            {
                var data = graphData.Nodes[i];
                var node = new FsmStateNode(data);
                node.OnBreakpointToggled += (n) =>
                {
                    OnBreakpointToggled?.Invoke(n.StateIdValue, n.HasBreakpoint);
                };
                _nodesByStateId[data.StateIdValue] = node;
                AddElement(node);
            }

            LayoutNodes(graphData.Nodes);

            // Create edges
            for (int i = 0; i < graphData.Edges.Count; i++)
            {
                var edgeData = graphData.Edges[i];
                if (!_nodesByStateId.TryGetValue(edgeData.FromStateIdValue, out var fromNode)) continue;
                if (!_nodesByStateId.TryGetValue(edgeData.ToStateIdValue, out var toNode)) continue;

                var edge = fromNode.OutputPort.ConnectTo(toNode.InputPort);

                if (edgeData.Kind == EdgeKind.DecisionBranch)
                {
                    edge.edgeControl.inputColor = new Color(0.9f, 0.7f, 0.2f);
                    edge.edgeControl.outputColor = new Color(0.9f, 0.7f, 0.2f);
                }

                edge.tooltip = edgeData.Label;

                _edges.Add(edge);
                AddElement(edge);
            }
        }

        /// <summary>
        /// Update visual state of all nodes from live machine data.
        /// Reads from FsmLiveState (a detached snapshot), sets visual properties.
        /// No state from this method feeds back into the FSM.
        /// </summary>
        public void UpdateLiveState(FsmLiveState liveState)
        {
            if (liveState == null || _cachedTopology == null)
            {
                ClearAllLiveState();
                return;
            }

            foreach (var kvp in _nodesByStateId)
            {
                bool isActive = kvp.Key == liveState.ActiveStateIdValue;
                kvp.Value.SetLiveState(
                    isActive,
                    liveState.Status,
                    liveState.BlockReason,
                    isActive ? liveState.ActiveStepIndex : -1,
                    liveState.WaitingForEventName,
                    liveState.WaitUntilTime);
            }

            HighlightRecentTransitions(liveState);
        }

        public void ClearAllLiveState()
        {
            foreach (var kvp in _nodesByStateId)
                kvp.Value.ClearLiveState();

            ResetEdgeColors();
        }

        /// <summary>
        /// Auto-layout: positions are cosmetic and ephemeral.
        /// They do not persist, serialize, or feed back into the FSM.
        /// </summary>
        private void LayoutNodes(List<NodeData> nodes)
        {
            float xSpacing = 280f;
            float ySpacing = 200f;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(nodes.Count)));

            for (int i = 0; i < nodes.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;

                if (_nodesByStateId.TryGetValue(nodes[i].StateIdValue, out var node))
                {
                    var pos = new Rect(100 + col * xSpacing, 100 + row * ySpacing, 220, 150);
                    node.SetPosition(pos);
                }
            }
        }

        private void HighlightRecentTransitions(FsmLiveState liveState)
        {
            ResetEdgeColors();

            // Highlight the last transition edge with reason overlay
            if (liveState.LastTransitionFromStateId >= 0 && liveState.LastTransitionToStateId >= 0)
            {
                for (int i = 0; i < _edges.Count && i < _cachedTopology.Edges.Count; i++)
                {
                    var edgeData = _cachedTopology.Edges[i];
                    if (edgeData.FromStateIdValue == liveState.LastTransitionFromStateId &&
                        edgeData.ToStateIdValue == liveState.LastTransitionToStateId)
                    {
                        _edges[i].edgeControl.inputColor = new Color(0.1f, 0.9f, 0.4f);
                        _edges[i].edgeControl.outputColor = new Color(0.1f, 0.9f, 0.4f);

                        // Transition reason overlay — shows provenance on the edge
                        var reason = liveState.LastTransitionReason ?? "";
                        var detail = liveState.LastTransitionDetail ?? edgeData.Label;
                        _edges[i].tooltip = $"{detail}\nReason: {reason}\nt={liveState.LastTransitionTimestamp:F2}s";
                    }
                }
            }
        }

        private void ResetEdgeColors()
        {
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_cachedTopology != null && i < _cachedTopology.Edges.Count)
                {
                    var kind = _cachedTopology.Edges[i].Kind;
                    var color = kind == EdgeKind.DecisionBranch
                        ? new Color(0.9f, 0.7f, 0.2f)
                        : new Color(0.6f, 0.6f, 0.6f);
                    _edges[i].edgeControl.inputColor = color;
                    _edges[i].edgeControl.outputColor = color;
                    _edges[i].tooltip = _cachedTopology.Edges[i].Label;
                }
            }
        }

        private void ClearGraph()
        {
            _nodesByStateId.Clear();
            _edges.Clear();
            _cachedTopology = null;
            graphElements.ForEach(RemoveElement);
        }

        /// <summary>
        /// Highlight a specific transition on the graph (for timeline scrubbing).
        /// Clears active node highlighting and shows the from/to states of the selected trace.
        /// </summary>
        public void HighlightTransition(TransitionRecord record)
        {
            if (record == null || _cachedTopology == null)
            {
                ClearAllLiveState();
                return;
            }

            // Dim all nodes, highlight from (faded) and to (active)
            foreach (var kvp in _nodesByStateId)
            {
                bool isFrom = kvp.Key == record.FromStateIdValue;
                bool isTo = kvp.Key == record.ToStateIdValue;

                if (isTo)
                    kvp.Value.SetLiveState(true, "Timeline", record.Reason, -1);
                else if (isFrom)
                    kvp.Value.SetLiveState(true, "Completed", "", -1);
                else
                    kvp.Value.ClearLiveState();
            }

            // Highlight the matching edge
            ResetEdgeColors();
            for (int i = 0; i < _edges.Count && i < _cachedTopology.Edges.Count; i++)
            {
                var edgeData = _cachedTopology.Edges[i];
                if (edgeData.FromStateIdValue == record.FromStateIdValue &&
                    edgeData.ToStateIdValue == record.ToStateIdValue)
                {
                    _edges[i].edgeControl.inputColor = new Color(0.1f, 0.9f, 0.4f);
                    _edges[i].edgeControl.outputColor = new Color(0.1f, 0.9f, 0.4f);
                    _edges[i].tooltip = $"{record.Detail}\nReason: {record.Reason}\nt={record.Timestamp:F2}s";
                }
            }
        }

        /// <summary>
        /// Return empty — users cannot create connections.
        /// The graph topology comes from MachineDefinition only.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return new List<Port>();
        }
    }
}