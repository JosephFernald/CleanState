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
        /// <summary>Human-readable name of this machine definition.</summary>
        public string Name { get; }
        /// <summary>The state the machine enters on start.</summary>
        public StateId InitialState { get; }
        /// <summary>Lookup table mapping identifiers to human-readable names.</summary>
        public NameLookup NameLookup { get; }

        private readonly StateDefinition[] _states;
        private readonly Dictionary<int, int> _stateIndexById;

        /// <summary>Creates a new immutable machine definition from the given states.</summary>
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

        /// <summary>Total number of states in this definition.</summary>
        public int StateCount => _states.Length;

        /// <summary>Gets the state definition for the given identifier, or throws if not found.</summary>
        public StateDefinition GetState(StateId id)
        {
            if (_stateIndexById.TryGetValue(id.Value, out int index))
                return _states[index];
            throw new System.ArgumentException($"State {id} not found in machine '{Name}'.");
        }

        /// <summary>Tries to get the state definition for the given identifier.</summary>
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

        /// <summary>Gets the state definition at the given array index.</summary>
        public StateDefinition GetStateByIndex(int index) => _states[index];
    }
}