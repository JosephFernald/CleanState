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
        public static readonly MachineId Invalid = new MachineId(-1);

        public readonly int Value;

        public MachineId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value >= 0;

        public bool Equals(MachineId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is MachineId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Machine({Value})";

        public static bool operator ==(MachineId left, MachineId right) => left.Value == right.Value;
        public static bool operator !=(MachineId left, MachineId right) => left.Value != right.Value;
    }
}