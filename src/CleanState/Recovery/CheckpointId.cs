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
        public static readonly CheckpointId Invalid = new CheckpointId(-1);

        public readonly int Value;

        public CheckpointId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value >= 0;

        public bool Equals(CheckpointId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CheckpointId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Checkpoint({Value})";

        public static bool operator ==(CheckpointId left, CheckpointId right) => left.Value == right.Value;
        public static bool operator !=(CheckpointId left, CheckpointId right) => left.Value != right.Value;
    }
}