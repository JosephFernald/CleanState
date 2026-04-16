// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CleanState.Debug;
using CleanState.Steps;

namespace CleanState.Builder
{
    /// <summary>
    /// Fluent builder for defining steps within a state.
    /// </summary>
    public sealed class StateBuilder
    {
        private readonly MachineBuilder _parent;
        internal readonly string StateName;
        internal bool IsCheckpoint;

        internal readonly List<PendingStep> Steps = new List<PendingStep>();
        internal readonly List<PendingDecision> Decisions = new List<PendingDecision>();

        internal StateBuilder(MachineBuilder parent, string stateName)
        {
            _parent = parent;
            StateName = stateName;
        }

        /// <summary>
        /// Mark this state as a recovery checkpoint.
        /// </summary>
        public StateBuilder Checkpoint()
        {
            IsCheckpoint = true;
            return this;
        }

        /// <summary>
        /// Add a step that runs on entry to this state.
        /// </summary>
        public StateBuilder TransitionIn(
            Action<MachineContext> action,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.Action, label ?? "TransitionIn", action, file, line));
            return this;
        }

        /// <summary>
        /// Add an action step that executes immediately and continues.
        /// </summary>
        public StateBuilder Then(
            Action<MachineContext> action,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.Action, label ?? "Then", action, file, line));
            return this;
        }

        /// <summary>
        /// Add a step that blocks until the specified event is received.
        /// </summary>
        public StateBuilder WaitForEvent(
            string eventName,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.WaitForEvent, label ?? $"WaitFor({eventName})", eventName: eventName, sourceFile: file, sourceLine: line));
            return this;
        }

        /// <summary>
        /// Add a step that blocks for the specified duration in seconds.
        /// </summary>
        public StateBuilder WaitForTime(
            double duration,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.WaitForTime, label ?? $"Wait({duration}s)", duration: duration, sourceFile: file, sourceLine: line));
            return this;
        }

        /// <summary>
        /// Add a step that blocks until a predicate returns true.
        /// </summary>
        public StateBuilder WaitUntil(
            Func<MachineContext, bool> predicate,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.WaitForPredicate, label ?? "WaitUntil", predicate: predicate, sourceFile: file, sourceLine: line));
            return this;
        }

        /// <summary>
        /// Add a direct transition to another state.
        /// </summary>
        public StateBuilder GoTo(
            string targetState,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            Steps.Add(new PendingStep(PendingStepKind.GoTo, label ?? $"GoTo({targetState})", targetState: targetState, sourceFile: file, sourceLine: line));
            return this;
        }

        /// <summary>
        /// Add a step that blocks until ALL specified conditions are satisfied.
        /// </summary>
        public StateBuilder WaitForAll(
            Action<WaitConditionBuilder> configure,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new WaitConditionBuilder();
            configure(builder);
            if (builder.Conditions.Count == 0)
                throw new InvalidOperationException("WaitForAll requires at least one condition.");
            Steps.Add(new PendingStep(PendingStepKind.WaitForAll, label ?? "WaitForAll", sourceFile: file, sourceLine: line, conditionBuilder: builder));
            return this;
        }

        /// <summary>
        /// Add a step that blocks until ANY of the specified conditions is satisfied.
        /// </summary>
        public StateBuilder WaitForAny(
            Action<WaitConditionBuilder> configure,
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new WaitConditionBuilder();
            configure(builder);
            if (builder.Conditions.Count == 0)
                throw new InvalidOperationException("WaitForAny requires at least one condition.");
            Steps.Add(new PendingStep(PendingStepKind.WaitForAny, label ?? "WaitForAny", sourceFile: file, sourceLine: line, conditionBuilder: builder));
            return this;
        }

        /// <summary>
        /// Begin a decision block with conditional branches.
        /// </summary>
        public DecisionBuilder Decision(
            string label = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            var builder = new DecisionBuilder(this, label ?? "Decision");
            // Record source location for the decision step
            Steps.Add(new PendingStep(PendingStepKind.DecisionPlaceholder, label ?? "Decision", sourceFile: file, sourceLine: line));
            return builder;
        }

        /// <summary>
        /// Start defining a new state (returns to parent builder).
        /// </summary>
        public StateBuilder State(string name) => _parent.State(name);

        /// <summary>
        /// Finish building and compile the machine definition.
        /// </summary>
        public Runtime.MachineDefinition Build() => _parent.Build();

        internal void AddPendingDecision(string label, List<DecisionBuilder.PendingBranch> branches)
        {
            Decisions.Add(new PendingDecision(label, branches));
        }

        internal enum PendingStepKind
        {
            Action,
            WaitForEvent,
            WaitForTime,
            WaitForPredicate,
            GoTo,
            DecisionPlaceholder,
            WaitForAll,
            WaitForAny
        }

        internal readonly struct PendingStep
        {
            public readonly PendingStepKind Kind;
            public readonly string Label;
            public readonly Action<MachineContext> Action;
            public readonly Func<MachineContext, bool> Predicate;
            public readonly string EventName;
            public readonly string TargetState;
            public readonly double Duration;
            public readonly string SourceFile;
            public readonly int SourceLine;
            public readonly WaitConditionBuilder ConditionBuilder;

            public PendingStep(
                PendingStepKind kind,
                string label,
                Action<MachineContext> action = null,
                string sourceFile = null,
                int sourceLine = 0,
                string eventName = null,
                string targetState = null,
                double duration = 0.0,
                Func<MachineContext, bool> predicate = null,
                WaitConditionBuilder conditionBuilder = null)
            {
                Kind = kind;
                Label = label;
                Action = action;
                Predicate = predicate;
                EventName = eventName;
                TargetState = targetState;
                Duration = duration;
                SourceFile = sourceFile;
                SourceLine = sourceLine;
                ConditionBuilder = conditionBuilder;
            }
        }

        internal readonly struct PendingDecision
        {
            public readonly string Label;
            public readonly List<DecisionBuilder.PendingBranch> Branches;

            public PendingDecision(string label, List<DecisionBuilder.PendingBranch> branches)
            {
                Label = label;
                Branches = branches;
            }
        }
    }
}