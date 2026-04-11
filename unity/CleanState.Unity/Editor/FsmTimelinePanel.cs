// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using CleanState.Unity.Runtime;

namespace CleanState.Unity.Editor
{
    /// <summary>
    /// Timeline panel that shows recent transitions from the trace buffer.
    /// Allows scrubbing through history and highlighting transitions on the graph.
    ///
    /// SOURCE OF TRUTH: TraceBuffer (via FsmLiveState.RecentTransitions).
    /// This panel is a disposable projection — rebuilt each frame from live data.
    /// No state in this panel feeds back into the FSM.
    /// </summary>
    public sealed class FsmTimelinePanel : VisualElement
    {
        private readonly ScrollView _scrollView;
        private readonly Label _headerLabel;
        private readonly List<TransitionRecord> _currentRecords = new List<TransitionRecord>();
        private int _selectedIndex = -1;
        private bool _isScrubbing;

        public bool IsScrubbing => _isScrubbing;
        public TransitionRecord SelectedRecord =>
            (_selectedIndex >= 0 && _selectedIndex < _currentRecords.Count)
                ? _currentRecords[_selectedIndex]
                : null;

        /// <summary>
        /// Raised when the user selects a transition in the timeline.
        /// The graph view should highlight that transition.
        /// </summary>
        public System.Action<TransitionRecord> OnTransitionSelected;

        /// <summary>
        /// Raised when the user exits scrub mode (returns to live).
        /// </summary>
        public System.Action OnReturnToLive;

        public FsmTimelinePanel()
        {
            style.height = 140;
            style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.height = 22;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            _headerLabel = new Label("Timeline — 0 transitions");
            _headerLabel.style.flexGrow = 1;
            _headerLabel.style.fontSize = 11;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            header.Add(_headerLabel);

            var liveButton = new Button(ReturnToLive) { text = "Live" };
            liveButton.style.width = 40;
            liveButton.style.height = 18;
            header.Add(liveButton);

            var prevButton = new Button(SelectPrev) { text = "\u25c0" };
            prevButton.style.width = 24;
            prevButton.style.height = 18;
            prevButton.style.marginLeft = 4;
            header.Add(prevButton);

            var nextButton = new Button(SelectNext) { text = "\u25b6" };
            nextButton.style.width = 24;
            nextButton.style.height = 18;
            nextButton.style.marginLeft = 2;
            header.Add(nextButton);

            Add(header);

            // Scrollable list of transitions
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            Add(_scrollView);
        }

        /// <summary>
        /// Update the timeline from live state data.
        /// If not scrubbing, rebuilds the entry list from RecentTransitions.
        /// </summary>
        public void UpdateFromLiveState(FsmLiveState liveState, NameResolver resolver)
        {
            if (liveState == null)
            {
                Clear();
                return;
            }

            _currentRecords.Clear();
            _currentRecords.AddRange(liveState.RecentTransitions);

            _headerLabel.text = $"Timeline — {_currentRecords.Count} transitions";

            // Rebuild the visual entries
            _scrollView.Clear();
            for (int i = 0; i < _currentRecords.Count; i++)
            {
                var record = _currentRecords[i];
                var entry = CreateEntry(i, record, resolver);
                _scrollView.Add(entry);
            }

            // If not scrubbing, auto-select the latest
            if (!_isScrubbing && _currentRecords.Count > 0)
            {
                _selectedIndex = _currentRecords.Count - 1;
                HighlightEntry(_selectedIndex);
            }
            else if (_isScrubbing && _selectedIndex >= 0)
            {
                HighlightEntry(_selectedIndex);
            }
        }

        public void Clear()
        {
            _currentRecords.Clear();
            _scrollView.Clear();
            _selectedIndex = -1;
            _isScrubbing = false;
            _headerLabel.text = "Timeline — 0 transitions";
        }

        private VisualElement CreateEntry(int index, TransitionRecord record, NameResolver resolver)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 20;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.alignItems = Align.Center;

            var capturedIndex = index;
            row.RegisterCallback<ClickEvent>(evt =>
            {
                _isScrubbing = true;
                _selectedIndex = capturedIndex;
                HighlightEntry(capturedIndex);
                OnTransitionSelected?.Invoke(_currentRecords[capturedIndex]);
            });

            var fromName = resolver != null ? resolver(record.FromStateIdValue) : $"State({record.FromStateIdValue})";
            var toName = resolver != null ? resolver(record.ToStateIdValue) : $"State({record.ToStateIdValue})";

            var timeLabel = new Label($"t={record.Timestamp:F2}");
            timeLabel.style.width = 60;
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            row.Add(timeLabel);

            var transLabel = new Label($"{fromName} \u2192 {toName}");
            transLabel.style.flexGrow = 1;
            transLabel.style.fontSize = 10;
            transLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            row.Add(transLabel);

            var reasonLabel = new Label(record.Reason);
            reasonLabel.style.width = 100;
            reasonLabel.style.fontSize = 9;
            reasonLabel.style.color = ReasonColor(record.Reason);
            reasonLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(reasonLabel);

            var detailLabel = new Label(record.Detail ?? "");
            detailLabel.style.width = 120;
            detailLabel.style.fontSize = 9;
            detailLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
            detailLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(detailLabel);

            return row;
        }

        private void HighlightEntry(int index)
        {
            for (int i = 0; i < _scrollView.childCount; i++)
            {
                _scrollView[i].style.backgroundColor =
                    i == index
                        ? new Color(0.2f, 0.35f, 0.5f)
                        : Color.clear;
            }
        }

        private void SelectPrev()
        {
            if (_currentRecords.Count == 0) return;
            _isScrubbing = true;
            _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
            HighlightEntry(_selectedIndex);
            OnTransitionSelected?.Invoke(_currentRecords[_selectedIndex]);
        }

        private void SelectNext()
        {
            if (_currentRecords.Count == 0) return;
            _isScrubbing = true;
            _selectedIndex = Mathf.Min(_currentRecords.Count - 1, _selectedIndex + 1);
            HighlightEntry(_selectedIndex);
            OnTransitionSelected?.Invoke(_currentRecords[_selectedIndex]);
        }

        private void ReturnToLive()
        {
            _isScrubbing = false;
            _selectedIndex = _currentRecords.Count - 1;
            if (_selectedIndex >= 0)
                HighlightEntry(_selectedIndex);
            OnReturnToLive?.Invoke();
        }

        private static Color ReasonColor(string reason)
        {
            switch (reason)
            {
                case "Direct":              return new Color(0.5f, 0.7f, 0.9f);
                case "DecisionBranch":      return new Color(0.9f, 0.7f, 0.2f);
                case "EventReceived":       return new Color(0.3f, 0.9f, 0.4f);
                case "RecoveryRestore":     return new Color(0.9f, 0.4f, 0.4f);
                case "ForcedJump":          return new Color(0.9f, 0.3f, 0.3f);
                default:                    return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        /// <summary>
        /// Delegate to resolve state ID values to names.
        /// Supplied by the window from MachineDefinition.NameLookup.
        /// </summary>
        public delegate string NameResolver(int stateIdValue);
    }
}