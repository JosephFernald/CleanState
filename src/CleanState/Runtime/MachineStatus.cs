// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace CleanState.Runtime
{
    public enum MachineStatus
    {
        /// <summary>Machine has not started yet.</summary>
        Idle,

        /// <summary>Machine is actively executing steps.</summary>
        Running,

        /// <summary>Machine is blocked waiting for a condition.</summary>
        Blocked,

        /// <summary>Machine has completed all states.</summary>
        Completed,

        /// <summary>Machine encountered an error.</summary>
        Faulted
    }
}