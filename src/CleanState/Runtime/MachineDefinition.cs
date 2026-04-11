// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CleanState.Identity;

namespace CleanState.Runtime
{
    /// <summary>
    /// Compiled, immutable definition of a state machine.
    /// Built by the fluent API, consumed by Machine at runtime.
    /// </summary>
    public sealed class MachineDefinition
    {
        public string Name { get; }
        public StateId InitialState { get; }
        public NameLookup NameLookup { get; }

        private readonly StateDefinition[] _states;
        private readonly Dictionary<int, int> _stateIndexById;

        public MachineDefinition(string name, StateDefinition[] states, StateId initialState, NameLookup nameLookup)
        {
            Name = name;
            _states = states;
            InitialState = initialState;
            NameLookup = nameLookup;

            _stateIndexById = new Dictionary<int, int>(_states.Length);
            for (int i = 0; i < _states.Length; i++)
            {
                _stateIndexById[_states[i].Id.Value] = i;
            }
        }

        public int StateCount => _states.Length;

        public StateDefinition GetState(StateId id)
        {
            if (_stateIndexById.TryGetValue(id.Value, out int index))
                return _states[index];
            throw new System.ArgumentException($"State {id} not found in machine '{Name}'.");
        }

        public bool TryGetState(StateId id, out StateDefinition state)
        {
            if (_stateIndexById.TryGetValue(id.Value, out int index))
            {
                state = _states[index];
                return true;
            }
            state = null;
            return false;
        }

        public StateDefinition GetStateByIndex(int index) => _states[index];
    }
}