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
        public StateId Id { get; }
        public string Name { get; }
        public IStep[] Steps { get; }
        public bool IsCheckpoint { get; }

        public StateDefinition(StateId id, string name, IStep[] steps, bool isCheckpoint)
        {
            Id = id;
            Name = name;
            Steps = steps;
            IsCheckpoint = isCheckpoint;
        }
    }
}