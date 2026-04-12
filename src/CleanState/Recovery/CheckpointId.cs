// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace CleanState.Recovery
{
    /// <summary>
    /// Typed identifier for a recovery checkpoint.
    /// </summary>
    public readonly struct CheckpointId : IEquatable<CheckpointId>
    {
        /// <summary>Sentinel value representing no checkpoint.</summary>
        public static readonly CheckpointId Invalid = new CheckpointId(-1);

        /// <summary>The underlying integer identifier.</summary>
        public readonly int Value;

        /// <summary>Creates a checkpoint identifier with the given value.</summary>
        public CheckpointId(int value)
        {
            Value = value;
        }

        /// <summary>Returns true if this identifier represents a valid checkpoint.</summary>
        public bool IsValid => Value >= 0;

        /// <inheritdoc />
        public bool Equals(CheckpointId other) => Value == other.Value;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CheckpointId other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Value;
        /// <inheritdoc />
        public override string ToString() => $"Checkpoint({Value})";

        /// <summary>Returns true if both identifiers have the same value.</summary>
        public static bool operator ==(CheckpointId left, CheckpointId right) => left.Value == right.Value;
        /// <summary>Returns true if the identifiers have different values.</summary>
        public static bool operator !=(CheckpointId left, CheckpointId right) => left.Value != right.Value;
    }
}