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
    /// GraphView node representing a single FSM state.
    /// This is a disposable projection of a StateDefinition — not a source of truth.
    /// Rebuilt from MachineDefinition each time the graph is refreshed.
    /// No state in this node feeds back into the FSM.
    /// </summary>
    public sealed class FsmStateNode : Node
    {
        public int StateIdValue { get; }
        public string StateName { get; }

        private readonly VisualElement _statusBar;
        private readonly Label _statusLabel;
        private readonly VisualElement _stepsContainer;
        private readonly StepData[] _originalSteps;

        private static readonly Color ColorIdle = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color ColorActive = new Color(0.1f, 0.7f, 0.3f);
        private static readonly Color ColorBlocked = new Color(0.9f, 0.6f, 0.1f);
        private static readonly Color ColorCompleted = new Color(0.3f, 0.5f, 0.8f);
        private static readonly Color ColorFaulted = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ColorCheckpoint = new Color(0.2f, 0.4f, 0.7f);

        // Step type colors
        private static readonly Color ColorStepAction = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color ColorStepWait = new Color(0.7f, 0.55f, 0.2f);
        private static readonly Color ColorStepDecision = new Color(0.6f, 0.5f, 0.8f);
        private static readonly Color ColorStepTransition = new Color(0.4f, 0.65f, 0.8f);

        public Port InputPort { get; }
        public Port OutputPort { get; }

        private readonly VisualElement _breakpointIndicator;
        private bool _hasBreakpoint;

        /// <summary>True if a breakpoint is set on this state.</summary>
        public bool HasBreakpoint => _hasBreakpoint;

        /// <summary>Raised when the user clicks the breakpoint indicator.</summary>
        public System.Action<FsmStateNode> OnBreakpointToggled;

        public FsmStateNode(NodeData data)
        {
            StateIdValue = data.StateIdValue;
            StateName = data.Name;

            title = data.Name;

            // Prevent mutation from the graph UI — this is a projection, not an editor
            capabilities &= ~Capabilities.Deletable;
            capabilities &= ~Capabilities.Copiable;
            capabilities &= ~Capabilities.Renamable;

            // Status bar at the top
            _statusBar = new VisualElement();
            _statusBar.style.height = 4;
            _statusBar.style.backgroundColor = ColorIdle;
            _statusBar.style.marginBottom = 2;
            mainContainer.Insert(0, _statusBar);

            // Badges
            if (data.IsInitial)
            {
                var badge = new Label("START");
                badge.style.fontSize = 9;
                badge.style.color = new Color(0.3f, 0.9f, 0.4f);
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.paddingLeft = 4;
                badge.style.paddingRight = 4;
                titleContainer.Add(badge);
            }

            if (data.IsCheckpoint)
            {
                var badge = new Label("CP");
                badge.style.fontSize = 9;
                badge.style.color = ColorCheckpoint;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.paddingLeft = 4;
                badge.style.paddingRight = 4;
                titleContainer.Add(badge);
            }

            // Status label
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _statusLabel.style.paddingLeft = 8;
            _statusLabel.style.paddingBottom = 4;
            mainContainer.Add(_statusLabel);

            // Steps list — store originals for rebuild each frame
            _originalSteps = new StepData[data.Steps.Count];
            _stepsContainer = new VisualElement();
            _stepsContainer.style.paddingLeft = 8;
            _stepsContainer.style.paddingRight = 8;
            _stepsContainer.style.paddingBottom = 4;

            for (int i = 0; i < data.Steps.Count; i++)
            {
                _originalSteps[i] = data.Steps[i];
                var tag = StepTypeTag(data.Steps[i].StepType);
                var stepLabel = new Label($"{tag} {data.Steps[i].Label}");
                stepLabel.style.fontSize = 10;
                stepLabel.style.color = StepTypeColor(data.Steps[i].StepType);
                _stepsContainer.Add(stepLabel);
            }
            mainContainer.Add(_stepsContainer);

            // Breakpoint indicator (click to toggle)
            _breakpointIndicator = new VisualElement();
            _breakpointIndicator.style.width = 12;
            _breakpointIndicator.style.height = 12;
            _breakpointIndicator.style.borderTopLeftRadius = 6;
            _breakpointIndicator.style.borderTopRightRadius = 6;
            _breakpointIndicator.style.borderBottomLeftRadius = 6;
            _breakpointIndicator.style.borderBottomRightRadius = 6;
            _breakpointIndicator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            _breakpointIndicator.style.position = Position.Absolute;
            _breakpointIndicator.style.left = -8;
            _breakpointIndicator.style.top = 8;
            _breakpointIndicator.tooltip = "Click to toggle breakpoint";
            _breakpointIndicator.RegisterCallback<ClickEvent>(evt =>
            {
                _hasBreakpoint = !_hasBreakpoint;
                UpdateBreakpointVisual();
                OnBreakpointToggled?.Invoke(this);
            });
            mainContainer.Add(_breakpointIndicator);

            // Ports
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "";
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "";
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// Update the visual state to reflect the machine's current execution.
        /// Rebuilds display text from stored originals each call — never accumulates state.
        /// </summary>
        public void SetLiveState(bool isActive, string status, string blockReason, int activeStepIndex,
            string waitingForEventName = null, float waitUntilTime = 0f)
        {
            if (isActive)
            {
                switch (status)
                {
                    case "Running":
                        _statusBar.style.backgroundColor = ColorActive;
                        _statusLabel.text = "Running";
                        break;
                    case "Blocked":
                        _statusBar.style.backgroundColor = ColorBlocked;
                        _statusLabel.text = FormatBlockReason(blockReason, waitingForEventName, waitUntilTime);
                        break;
                    case "Completed":
                        _statusBar.style.backgroundColor = ColorCompleted;
                        _statusLabel.text = "Completed";
                        break;
                    case "Faulted":
                        _statusBar.style.backgroundColor = ColorFaulted;
                        _statusLabel.text = "FAULTED";
                        break;
                    default:
                        _statusBar.style.backgroundColor = ColorActive;
                        _statusLabel.text = status;
                        break;
                }

                RebuildStepLabels(activeStepIndex);

                // Active border
                style.borderLeftWidth = 2;
                style.borderRightWidth = 2;
                style.borderTopWidth = 2;
                style.borderBottomWidth = 2;
                style.borderLeftColor = ColorActive;
                style.borderRightColor = ColorActive;
                style.borderTopColor = ColorActive;
                style.borderBottomColor = ColorActive;
            }
            else
            {
                _statusBar.style.backgroundColor = ColorIdle;
                _statusLabel.text = "";
                style.borderLeftWidth = 0;
                style.borderRightWidth = 0;
                style.borderTopWidth = 0;
                style.borderBottomWidth = 0;

                RebuildStepLabels(-1);
            }
        }

        public void ClearLiveState()
        {
            SetLiveState(false, "", "", -1);
        }

        /// <summary>
        /// Set breakpoint state programmatically (e.g., from the debug controller).
        /// </summary>
        public void SetBreakpoint(bool active)
        {
            _hasBreakpoint = active;
            UpdateBreakpointVisual();
        }

        private void UpdateBreakpointVisual()
        {
            _breakpointIndicator.style.backgroundColor = _hasBreakpoint
                ? new Color(0.9f, 0.15f, 0.15f)
                : new Color(0.3f, 0.3f, 0.3f);
        }

        private string FormatBlockReason(string blockKind, string eventName, float waitUntil)
        {
            switch (blockKind)
            {
                case "WaitForEvent":
                    return eventName != null
                        ? $"Waiting for event: {eventName}"
                        : "Waiting for event";
                case "WaitForTime":
                    return $"Waiting until t={waitUntil:F1}s";
                case "WaitForPredicate":
                    return "Waiting for condition";
                case "WaitForChildMachine":
                    return "Waiting for child machine";
                default:
                    return $"Blocked: {blockKind}";
            }
        }

        /// <summary>
        /// Rebuild all step label text from the stored originals.
        /// Active step gets the arrow prefix and highlight. All others reset.
        /// This is called every frame — never reads from the label's current text.
        /// </summary>
        private void RebuildStepLabels(int activeStepIndex)
        {
            for (int i = 0; i < _stepsContainer.childCount && i < _originalSteps.Length; i++)
            {
                var child = _stepsContainer[i] as Label;
                if (child == null) continue;

                var tag = StepTypeTag(_originalSteps[i].StepType);

                if (i == activeStepIndex)
                {
                    child.text = $"\u25b6 {tag} {_originalSteps[i].Label}";
                    child.style.color = Color.white;
                    child.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    child.text = $"  {tag} {_originalSteps[i].Label}";
                    child.style.color = StepTypeColor(_originalSteps[i].StepType);
                    child.style.unityFontStyleAndWeight = FontStyle.Normal;
                }
            }
        }

        private static string StepTypeTag(string stepType)
        {
            if (stepType == null) return "  ";
            switch (stepType)
            {
                case "Action":              return "\u2022"; // bullet
                case "WaitForEvent":        return "\u29D6"; // hourglass
                case "WaitForTime":         return "\u23F1"; // stopwatch
                case "WaitForPredicate":    return "?";
                case "DecisionPlaceholder": return "\u2662"; // diamond
                case "GoTo":                return "\u2192"; // arrow
                default:                    return "\u2022";
            }
        }

        private static Color StepTypeColor(string stepType)
        {
            if (stepType == null) return ColorStepAction;
            switch (stepType)
            {
                case "Action":              return ColorStepAction;
                case "WaitForEvent":
                case "WaitForTime":
                case "WaitForPredicate":    return ColorStepWait;
                case "DecisionPlaceholder": return ColorStepDecision;
                case "GoTo":                return ColorStepTransition;
                default:                    return ColorStepAction;
            }
        }
    }
}