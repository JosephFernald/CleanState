// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace CleanState.Identity
{
    /// <summary>
    /// Typed identifier for an event that can be sent to a machine.
    /// Int-backed struct to avoid string comparisons at runtime.
    /// </summary>
    public readonly struct EventId : IEquatable<EventId>
    {
        /// <summary>Sentinel value representing no valid event.</summary>
        public static readonly EventId Invalid = new EventId(-1);

        /// <summary>The underlying integer value of this identifier.</summary>
        public readonly int Value;

        /// <summary>Initializes a new <see cref="EventId"/> with the specified integer value.</summary>
        public EventId(int value)
        {
            Value = value;
        }

        /// <summary>Gets a value indicating whether this identifier is valid (non-negative).</summary>
        public bool IsValid => Value >= 0;

        /// <inheritdoc />
        public bool Equals(EventId other) => Value == other.Value;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is EventId other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Value;
        /// <inheritdoc />
        public override string ToString() => $"Event({Value})";

        /// <summary>Returns <c>true</c> if two <see cref="EventId"/> values are equal.</summary>
        public static bool operator ==(EventId left, EventId right) => left.Value == right.Value;
        /// <summary>Returns <c>true</c> if two <see cref="EventId"/> values are not equal.</summary>
        public static bool operator !=(EventId left, EventId right) => left.Value != right.Value;
    }
}