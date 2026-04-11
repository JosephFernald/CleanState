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
        public static readonly StateId Invalid = new StateId(-1);

        public readonly int Value;

        public StateId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value >= 0;

        public bool Equals(StateId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StateId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"State({Value})";

        public static bool operator ==(StateId left, StateId right) => left.Value == right.Value;
        public static bool operator !=(StateId left, StateId right) => left.Value != right.Value;
    }
}