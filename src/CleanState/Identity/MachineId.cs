// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace CleanState.Identity
{
    /// <summary>
    /// Typed identifier for a machine instance within the scheduler.
    /// </summary>
    public readonly struct MachineId : IEquatable<MachineId>
    {
        /// <summary>Sentinel value representing no valid machine.</summary>
        public static readonly MachineId Invalid = new MachineId(-1);

        /// <summary>The underlying integer value of this identifier.</summary>
        public readonly int Value;

        /// <summary>Initializes a new <see cref="MachineId"/> with the specified integer value.</summary>
        public MachineId(int value)
        {
            Value = value;
        }

        /// <summary>Gets a value indicating whether this identifier is valid (non-negative).</summary>
        public bool IsValid => Value >= 0;

        /// <inheritdoc />
        public bool Equals(MachineId other) => Value == other.Value;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is MachineId other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Value;
        /// <inheritdoc />
        public override string ToString() => $"Machine({Value})";

        /// <summary>Returns <c>true</c> if two <see cref="MachineId"/> values are equal.</summary>
        public static bool operator ==(MachineId left, MachineId right) => left.Value == right.Value;
        /// <summary>Returns <c>true</c> if two <see cref="MachineId"/> values are not equal.</summary>
        public static bool operator !=(MachineId left, MachineId right) => left.Value != right.Value;
    }
}