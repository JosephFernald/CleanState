// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>Describes the type of a sub-condition inside a composite wait.</summary>
    public enum CompositeConditionKind
    {
        /// <summary>Wait until a specific event is received.</summary>
        Event,
        /// <summary>Wait until a duration has elapsed.</summary>
        Time,
        /// <summary>Wait until a predicate returns true.</summary>
        Predicate
    }

    /// <summary>
    /// A single sub-condition used by <see cref="WaitForAllStep"/> and <see cref="WaitForAnyStep"/>.
    /// Immutable after construction.
    /// </summary>
    public sealed class CompositeCondition
    {
        /// <summary>The kind of condition.</summary>
        public readonly CompositeConditionKind Kind;
        /// <summary>The event to wait for (only valid when Kind is Event).</summary>
        public readonly EventId EventId;
        /// <summary>The duration in seconds to wait (only valid when Kind is Time).</summary>
        public readonly double Duration;
        /// <summary>The predicate to evaluate (only valid when Kind is Predicate).</summary>
        public readonly Func<MachineContext, bool> Predicate;

        private CompositeCondition(CompositeConditionKind kind, EventId eventId, double duration, Func<MachineContext, bool> predicate)
        {
            Kind = kind;
            EventId = eventId;
            Duration = duration;
            Predicate = predicate;
        }

        /// <summary>Creates an event sub-condition.</summary>
        public static CompositeCondition ForEvent(EventId eventId) =>
            new CompositeCondition(CompositeConditionKind.Event, eventId, 0.0, null);

        /// <summary>Creates a time sub-condition.</summary>
        public static CompositeCondition ForTime(double duration) =>
            new CompositeCondition(CompositeConditionKind.Time, EventId.Invalid, duration, null);

        /// <summary>Creates a predicate sub-condition.</summary>
        public static CompositeCondition ForPredicate(Func<MachineContext, bool> predicate) =>
            new CompositeCondition(CompositeConditionKind.Predicate, EventId.Invalid, 0.0, predicate ?? throw new ArgumentNullException(nameof(predicate)));
    }
}
