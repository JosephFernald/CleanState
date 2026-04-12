// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace CleanState.Identity
{
    /// <summary>
    /// Typed identifier for a state within a machine definition.
    /// Int-backed struct to avoid string comparisons at runtime.
    /// </summary>
    public readonly struct StateId : IEquatable<StateId>
    {
        /// <summary>Sentinel value representing no valid state.</summary>
        public static readonly StateId Invalid = new StateId(-1);

        /// <summary>The underlying integer value of this identifier.</summary>
        public readonly int Value;

        /// <summary>Initializes a new <see cref="StateId"/> with the specified integer value.</summary>
        public StateId(int value)
        {
            Value = value;
        }

        /// <summary>Gets a value indicating whether this identifier is valid (non-negative).</summary>
        public bool IsValid => Value >= 0;

        /// <inheritdoc />
        public bool Equals(StateId other) => Value == other.Value;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is StateId other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Value;
        /// <inheritdoc />
        public override string ToString() => $"State({Value})";

        /// <summary>Returns <c>true</c> if two <see cref="StateId"/> values are equal.</summary>
        public static bool operator ==(StateId left, StateId right) => left.Value == right.Value;
        /// <summary>Returns <c>true</c> if two <see cref="StateId"/> values are not equal.</summary>
        public static bool operator !=(StateId left, StateId right) => left.Value != right.Value;
    }
}