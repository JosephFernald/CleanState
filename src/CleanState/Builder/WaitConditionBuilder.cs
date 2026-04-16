// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Steps;

namespace CleanState.Builder
{
    /// <summary>
    /// Fluent builder for composing sub-conditions used by WaitForAll / WaitForAny.
    /// </summary>
    public sealed class WaitConditionBuilder
    {
        internal readonly List<PendingCondition> Conditions = new List<PendingCondition>();

        /// <summary>Add a sub-condition that waits for the named event.</summary>
        public WaitConditionBuilder Event(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));
            Conditions.Add(new PendingCondition(PendingConditionKind.Event, eventName: eventName));
            return this;
        }

        /// <summary>Add a sub-condition that waits for a duration in seconds.</summary>
        public WaitConditionBuilder Time(double duration)
        {
            if (duration < 0)
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative.");
            Conditions.Add(new PendingCondition(PendingConditionKind.Time, duration: duration));
            return this;
        }

        /// <summary>Add a sub-condition that waits until a predicate returns true.</summary>
        public WaitConditionBuilder Predicate(Func<MachineContext, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            Conditions.Add(new PendingCondition(PendingConditionKind.Predicate, predicate: predicate));
            return this;
        }

        internal enum PendingConditionKind
        {
            Event,
            Time,
            Predicate
        }

        internal readonly struct PendingCondition
        {
            public readonly PendingConditionKind Kind;
            public readonly string EventName;
            public readonly double Duration;
            public readonly Func<MachineContext, bool> Predicate;

            public PendingCondition(
                PendingConditionKind kind,
                string eventName = null,
                double duration = 0.0,
                Func<MachineContext, bool> predicate = null)
            {
                Kind = kind;
                EventName = eventName;
                Duration = duration;
                Predicate = predicate;
            }
        }
    }
}
