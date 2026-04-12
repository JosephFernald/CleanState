// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace CleanState.Debug
{
    /// <summary>Describes why a state transition occurred.</summary>
    public enum TransitionReasonKind
    {
        /// <summary>Explicit transition to a named state.</summary>
        Direct,

        /// <summary>Transition chosen by a decision step branch.</summary>
        DecisionBranch,

        /// <summary>Transition triggered by receiving an event.</summary>
        EventReceived,

        /// <summary>Transition triggered by a timeout expiring.</summary>
        TimeoutElapsed,

        /// <summary>Transition triggered when a predicate became true.</summary>
        PredicateSatisfied,

        /// <summary>Transition triggered when a child machine completed.</summary>
        ChildMachineCompleted,

        /// <summary>Transition restoring state after a recovery operation.</summary>
        RecoveryRestore,

        /// <summary>Transition forced via debug controller jump.</summary>
        ForcedJump,

        /// <summary>Transition issued by an external command.</summary>
        ExternalCommand
    }
}