// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using CleanState.Debug;
using CleanState.Unity.Runtime;

namespace CleanState.Unity.Editor
{
    /// <summary>
    /// Editor window that hosts the FSM GraphView debugger.
    ///
    /// SOURCE OF TRUTH: MachineDefinition + IFsmObservable runtime state.
    /// This window is a disposable projection — it does not:
    ///   - Persist graph state across play sessions
    ///   - Serialize node positions or layout
    ///   - Store any data that feeds back into the FSM
    ///   - Survive play mode transitions (all state is invalidated)
    ///
    /// OBSERVATION ONLY:
    ///   - Reads IFsmObservable (read-only interface)
    ///   - Reads TraceBuffer (read-only snapshot)
    ///   - NEVER holds a raw Machine reference
    ///   - NEVER modifies MachineContext
    ///   - NEVER forces transitions except through FsmDebugController (explicit opt-in)
    /// </summary>
    public sealed class FsmGraphWindow : EditorWindow
    {
        private FsmGraphView _graphView;
        private FsmTimelinePanel _timelinePanel;
        private VisualElement _toolbar;
        private VisualElement _debugToolbar;
        private Label _machineLabel;
        private Label _statusLabel;
        private Button _prevButton;
        private Button _nextButton;
        private Button _refreshButton;
        private Button _pauseButton;
        private Button _stepButton;
        private Label _debugLabel;

        private int _selectedIndex = -1;
        private int _lastRegistryVersion = -1;
        private FsmGraphData _currentGraphData;
        private FsmTimelinePanel.NameResolver _nameResolver;

        // Breakpoint references keyed by state ID value — so we can remove them
        private readonly System.Collections.Generic.Dictionary<int, FsmBreakpoint> _nodeBreakpoints
            = new System.Collections.Generic.Dictionary<int, FsmBreakpoint>();

        [MenuItem("Window/CleanState/FSM Debugger")]
        public static void Open()
        {
            var window = GetWindow<FsmGraphWindow>();
            window.titleContent = new GUIContent("FSM Debugger");
            window.minSize = new Vector2(600, 500);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Machine selection toolbar
            _toolbar = new VisualElement();
            _toolbar.style.flexDirection = FlexDirection.Row;
            _toolbar.style.height = 28;
            _toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _toolbar.style.alignItems = Align.Center;
            _toolbar.style.paddingLeft = 8;
            _toolbar.style.paddingRight = 8;

            _prevButton = new Button(SelectPrevious) { text = "<" };
            _prevButton.style.width = 24;
            _toolbar.Add(_prevButton);

            _machineLabel = new Label("No machines registered");
            _machineLabel.style.flexGrow = 1;
            _machineLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _machineLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _toolbar.Add(_machineLabel);

            _nextButton = new Button(SelectNext) { text = ">" };
            _nextButton.style.width = 24;
            _toolbar.Add(_nextButton);

            _refreshButton = new Button(ForceRefresh) { text = "Rebuild" };
            _refreshButton.style.marginLeft = 8;
            _toolbar.Add(_refreshButton);

            root.Add(_toolbar);

            // Debug command toolbar
            _debugToolbar = new VisualElement();
            _debugToolbar.style.flexDirection = FlexDirection.Row;
            _debugToolbar.style.height = 24;
            _debugToolbar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _debugToolbar.style.alignItems = Align.Center;
            _debugToolbar.style.paddingLeft = 8;

            _pauseButton = new Button(TogglePause) { text = "Pause" };
            _pauseButton.style.width = 60;
            _debugToolbar.Add(_pauseButton);

            _stepButton = new Button(StepOnce) { text = "Step" };
            _stepButton.style.width = 50;
            _stepButton.style.marginLeft = 4;
            _debugToolbar.Add(_stepButton);

            _debugLabel = new Label("Debug commands: not available");
            _debugLabel.style.flexGrow = 1;
            _debugLabel.style.fontSize = 10;
            _debugLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _debugLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _debugLabel.style.paddingRight = 8;
            _debugToolbar.Add(_debugLabel);

            root.Add(_debugToolbar);

            // Status bar
            _statusLabel = new Label("");
            _statusLabel.style.height = 20;
            _statusLabel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _statusLabel.style.paddingLeft = 8;
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            root.Add(_statusLabel);

            // Graph view (takes remaining space)
            _graphView = new FsmGraphView();
            _graphView.style.flexGrow = 1;
            root.Add(_graphView);

            // Wire breakpoint toggling from graph nodes
            _graphView.OnBreakpointToggled += OnNodeBreakpointToggled;

            // Timeline panel (fixed at bottom)
            _timelinePanel = new FsmTimelinePanel();
            _timelinePanel.OnTransitionSelected += OnTimelineTransitionSelected;
            _timelinePanel.OnReturnToLive += OnTimelineReturnToLive;
            root.Add(_timelinePanel);

            UpdateDebugToolbarState();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                _selectedIndex = -1;
                _lastRegistryVersion = -1;
                _currentGraphData = null;
                _nameResolver = null;
                _graphView?.BuildFromGraphData(null);
                _timelinePanel?.Clear();
                if (_machineLabel != null)
                    _machineLabel.text = "No machines registered";
                if (_statusLabel != null)
                    _statusLabel.text = "Not playing";
            }
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying)
            {
                if (_currentGraphData != null)
                {
                    _currentGraphData = null;
                    _nameResolver = null;
                    _graphView.BuildFromGraphData(null);
                    _timelinePanel.Clear();
                    _statusLabel.text = "Not playing";
                }
                return;
            }

            if (FsmDebugRegistry.Version != _lastRegistryVersion)
            {
                _lastRegistryVersion = FsmDebugRegistry.Version;
                OnRegistryChanged();
            }

            if (_selectedIndex >= 0 && _selectedIndex < FsmDebugRegistry.Count)
            {
                var tracked = FsmDebugRegistry.Get(_selectedIndex);
                var live = tracked.TraceBuffer != null
                    ? tracked.Observable.ExtractLiveState(tracked.TraceBuffer)
                    : tracked.Observable.ExtractLiveState();

                // If timeline is scrubbing, don't update the graph from live — timeline controls it
                if (!_timelinePanel.IsScrubbing)
                {
                    _graphView.UpdateLiveState(live);
                }

                // Always update the timeline with latest data
                _timelinePanel.UpdateFromLiveState(live, _nameResolver);

                // Status bar
                var stepInfo = live.CurrentStepLabel != null
                    ? $"{live.ActiveStepIndex}/{live.StepCountInActiveState} [{live.CurrentStepType}] {live.CurrentStepLabel}"
                    : $"{live.ActiveStepIndex}/{live.StepCountInActiveState}";
                var blockInfo = live.BlockReason == "None" ? "" : $"  |  Block: {FormatBlockReason(live)}";
                var transInfo = live.LastTransitionReason != null
                    ? $"  |  Last: {live.LastTransitionDetail} ({live.LastTransitionReason})"
                    : "";
                _statusLabel.text = _timelinePanel.IsScrubbing
                    ? $"TIMELINE SCRUB  |  {_statusLabel.text}"
                    : $"State: {live.ActiveStateName}  |  Step: {stepInfo}{blockInfo}{transInfo}";

                UpdateDebugToolbarState();
                Repaint();
            }
        }

        // --- Timeline callbacks ---

        private void OnTimelineTransitionSelected(TransitionRecord record)
        {
            _graphView.HighlightTransition(record);
        }

        private void OnTimelineReturnToLive()
        {
            // Next OnEditorUpdate will refresh from live state
        }

        // --- Machine selection ---

        private void OnRegistryChanged()
        {
            if (FsmDebugRegistry.Count == 0)
            {
                _selectedIndex = -1;
                _machineLabel.text = "No machines registered";
                _currentGraphData = null;
                _nameResolver = null;
                _graphView.BuildFromGraphData(null);
                _timelinePanel.Clear();
                return;
            }

            if (_selectedIndex < 0 || _selectedIndex >= FsmDebugRegistry.Count)
                SelectMachine(0);
        }

        private void SelectMachine(int index)
        {
            if (index < 0 || index >= FsmDebugRegistry.Count) return;

            _selectedIndex = index;
            var tracked = FsmDebugRegistry.Get(index);
            var definition = tracked.Observable.Definition;

            _currentGraphData = definition.ExtractGraphData();
            _graphView.BuildFromGraphData(_currentGraphData);
            _timelinePanel.Clear();
            _nodeBreakpoints.Clear();

            // Build name resolver from the definition's lookup
            var lookup = definition.NameLookup;
            _nameResolver = (stateIdValue) =>
            {
                var id = new CleanState.Identity.StateId(stateIdValue);
                return lookup.GetStateName(id);
            };

            _machineLabel.text = $"{definition.Name}  ({index + 1}/{FsmDebugRegistry.Count})";
            UpdateDebugToolbarState();
        }

        private void SelectPrevious()
        {
            if (FsmDebugRegistry.Count == 0) return;
            SelectMachine((_selectedIndex - 1 + FsmDebugRegistry.Count) % FsmDebugRegistry.Count);
        }

        private void SelectNext()
        {
            if (FsmDebugRegistry.Count == 0) return;
            SelectMachine((_selectedIndex + 1) % FsmDebugRegistry.Count);
        }

        private void ForceRefresh()
        {
            _lastRegistryVersion = -1;
            if (_selectedIndex >= 0)
                SelectMachine(_selectedIndex);
        }

        // --- Breakpoints (toggled from graph nodes, managed via FsmDebugController) ---

        private void OnNodeBreakpointToggled(int stateIdValue, bool active)
        {
            var ctrl = GetSelectedDebugController();
            if (ctrl == null) return;

            if (active)
            {
                var stateId = new CleanState.Identity.StateId(stateIdValue);
                var bp = FsmBreakpoint.OnStateEntry(stateId);
                ctrl.AddBreakpoint(bp);
                _nodeBreakpoints[stateIdValue] = bp;
            }
            else
            {
                if (_nodeBreakpoints.TryGetValue(stateIdValue, out var bp))
                {
                    ctrl.RemoveBreakpoint(bp);
                    _nodeBreakpoints.Remove(stateIdValue);
                }
            }
        }

        // --- Debug commands (only via FsmDebugController, never raw Machine) ---

        private FsmDebugController GetSelectedDebugController()
        {
            if (_selectedIndex < 0 || _selectedIndex >= FsmDebugRegistry.Count)
                return null;
            return FsmDebugRegistry.Get(_selectedIndex).DebugController;
        }

        private void TogglePause()
        {
            var ctrl = GetSelectedDebugController();
            if (ctrl == null) return;

            if (ctrl.IsPaused)
                ctrl.Resume();
            else
                ctrl.Pause();

            UpdateDebugToolbarState();
        }

        private void StepOnce()
        {
            var ctrl = GetSelectedDebugController();
            if (ctrl == null || !ctrl.IsPaused) return;

            ctrl.StepOnce();
        }

        private static string FormatBlockReason(FsmLiveState live)
        {
            switch (live.BlockReason)
            {
                case "WaitForEvent":
                    return live.WaitingForEventName != null
                        ? $"event:{live.WaitingForEventName}"
                        : "event";
                case "WaitForTime":
                    return $"time(t={live.WaitUntilTime:F1}s)";
                case "WaitForPredicate":
                    return "condition";
                case "WaitForChildMachine":
                    return "child";
                default:
                    return live.BlockReason;
            }
        }

        private void UpdateDebugToolbarState()
        {
            var ctrl = GetSelectedDebugController();
            bool available = ctrl != null;

            _pauseButton.SetEnabled(available);
            _stepButton.SetEnabled(available && ctrl.IsPaused);

            if (!available)
            {
                _debugLabel.text = "Debug commands: not available (enable on FsmRunner)";
                _pauseButton.text = "Pause";
            }
            else if (ctrl.BreakpointHit && ctrl.LastHitBreakpoint != null)
            {
                _pauseButton.text = "Resume";
                _debugLabel.text = $"BREAKPOINT HIT — {ctrl.LastHitBreakpoint.Kind}";
            }
            else
            {
                _pauseButton.text = ctrl.IsPaused ? "Resume" : "Pause";
                _debugLabel.text = ctrl.IsPaused
                    ? "PAUSED"
                    : $"Live ({ctrl.BreakpointCount} breakpoints)";
            }
        }
    }
}