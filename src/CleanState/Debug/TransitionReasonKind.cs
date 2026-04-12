// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace CleanState.Debug
{
    public enum TransitionReasonKind
    {
        Direct,
        DecisionBranch,
        EventReceived,
        TimeoutElapsed,
        PredicateSatisfied,
        ChildMachineCompleted,
        RecoveryRestore,
        ForcedJump,
        ExternalCommand
    }
}