// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace CleanState.Identity
{
    /// <summary>
    /// Reverse lookup table mapping typed IDs back to their authored names.
    /// Used for debugging and tracing only — not consulted in runtime hot paths.
    /// </summary>
    public sealed class NameLookup
    {
        private readonly Dictionary<int, string> _stateNames = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _eventNames = new Dictionary<int, string>();

        /// <summary>Registers a human-readable name for the given state identifier.</summary>
        public void RegisterState(StateId id, string name)
        {
            _stateNames[id.Value] = name;
        }

        /// <summary>Registers a human-readable name for the given event identifier.</summary>
        public void RegisterEvent(EventId id, string name)
        {
            _eventNames[id.Value] = name;
        }

        /// <summary>Returns the registered name for a state, or its numeric string if unregistered.</summary>
        public string GetStateName(StateId id)
        {
            return _stateNames.TryGetValue(id.Value, out var name) ? name : id.ToString();
        }

        /// <summary>Returns the registered name for an event, or its numeric string if unregistered.</summary>
        public string GetEventName(EventId id)
        {
            return _eventNames.TryGetValue(id.Value, out var name) ? name : id.ToString();
        }
    }
}