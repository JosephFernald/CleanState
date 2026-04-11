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
        public static readonly EventId Invalid = new EventId(-1);

        public readonly int Value;

        public EventId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value >= 0;

        public bool Equals(EventId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EventId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Event({Value})";

        public static bool operator ==(EventId left, EventId right) => left.Value == right.Value;
        public static bool operator !=(EventId left, EventId right) => left.Value != right.Value;
    }
}