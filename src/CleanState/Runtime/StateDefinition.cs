// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;
using CleanState.Steps;

namespace CleanState.Runtime
{
    /// <summary>
    /// Compiled runtime representation of a state.
    /// Contains a flat array of steps and metadata.
    /// </summary>
    public sealed class StateDefinition
    {
        /// <summary>Unique identifier for this state.</summary>
        public StateId Id { get; }
        /// <summary>Human-readable name of this state.</summary>
        public string Name { get; }
        /// <summary>Ordered array of steps executed when the machine enters this state.</summary>
        public IStep[] Steps { get; }
        /// <summary>Whether this state is a recovery checkpoint.</summary>
        public bool IsCheckpoint { get; }

        /// <summary>Creates a new state definition with the given steps and metadata.</summary>
        public StateDefinition(StateId id, string name, IStep[] steps, bool isCheckpoint)
        {
            Id = id;
            Name = name;
            Steps = steps;
            IsCheckpoint = isCheckpoint;
        }
    }
}