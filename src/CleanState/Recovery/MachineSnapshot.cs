// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CleanState.Identity;

namespace CleanState.Recovery
{
    /// <summary>
    /// Serializable snapshot of machine state for recovery after interruption.
    /// Contains the logical phase (checkpoint state) and domain data.
    /// </summary>
    public sealed class MachineSnapshot
    {
        /// <summary>Name of the machine definition this snapshot belongs to.</summary>
        public string MachineName { get; set; }

        /// <summary>The state the machine was in at snapshot time.</summary>
        public string StateName { get; set; }

        /// <summary>Step index within the state (usually 0 for checkpoint recovery).</summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Domain data captured at the checkpoint.
        /// Keys and values should be serializable.
        /// </summary>
        public Dictionary<string, object> DomainData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Handles creating and restoring machine snapshots.
    /// </summary>
    public static class MachineRecovery
    {
        /// <summary>
        /// Capture a snapshot of the machine's current state and context data.
        /// Should be called at checkpoint states.
        /// </summary>
        public static MachineSnapshot CaptureSnapshot(Runtime.Machine machine, params string[] dataKeys)
        {
            var snapshot = new MachineSnapshot
            {
                MachineName = machine.Definition.Name,
                StateName = machine.Definition.NameLookup.GetStateName(machine.CurrentState),
                StepIndex = 0 // checkpoints always restore to step 0
            };

            var context = machine.Context;
            for (int i = 0; i < dataKeys.Length; i++)
            {
                if (context.TryGet<object>(dataKeys[i], out var value))
                {
                    snapshot.DomainData[dataKeys[i]] = value;
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Restore a machine from a snapshot. Restores domain data to context,
        /// then forces the machine into the checkpoint state.
        /// </summary>
        public static void RestoreFromSnapshot(Runtime.Machine machine, MachineSnapshot snapshot, double currentTime)
        {
            // Restore domain data
            foreach (var kvp in snapshot.DomainData)
            {
                machine.Context.Set(kvp.Key, kvp.Value);
            }

            // Find the state by name and force-enter it
            var nameLookup = machine.Definition.NameLookup;
            StateId targetState = StateId.Invalid;

            for (int i = 0; i < machine.Definition.StateCount; i++)
            {
                var state = machine.Definition.GetStateByIndex(i);
                if (state.Name == snapshot.StateName)
                {
                    targetState = state.Id;
                    break;
                }
            }

            if (!targetState.IsValid)
                throw new System.ArgumentException(
                    $"Cannot restore: state '{snapshot.StateName}' not found in machine '{machine.Definition.Name}'.");

            machine.ForceState(targetState, currentTime, snapshot.StepIndex);
        }
    }
}