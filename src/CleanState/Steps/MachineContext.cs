// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Identity;

namespace CleanState.Steps
{
    /// <summary>
    /// Context passed to steps during execution. Provides access to machine state
    /// without exposing the full machine internals.
    /// </summary>
    public sealed class MachineContext
    {
        /// <summary>The current time as provided by the machine's time source.</summary>
        public double CurrentTime { get; internal set; }
        /// <summary>The most recently received event, or EventId.Invalid if none.</summary>
        public EventId LastReceivedEvent { get; internal set; } = EventId.Invalid;
        /// <summary>The state the machine is currently in.</summary>
        public StateId CurrentState { get; internal set; }

        /// <summary>
        /// Shared blackboard for domain data. Keyed by string for flexibility,
        /// but lookups only happen in action steps, not hot-path scheduling.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<string, object> _data
            = new System.Collections.Generic.Dictionary<string, object>();

        /// <summary>Stores a value in the shared data store under the specified key.</summary>
        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        /// <summary>Retrieves a value by key, throwing if not found.</summary>
        public T Get<T>(string key)
        {
            return (T)_data[key];
        }

        /// <summary>Attempts to retrieve a value by key, returning false if not found.</summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var obj))
            {
                value = (T)obj;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Returns true if the data store contains the specified key.</summary>
        public bool Has(string key) => _data.ContainsKey(key);

        /// <summary>Removes the entry with the specified key from the data store.</summary>
        public void Remove(string key) => _data.Remove(key);

        /// <summary>Removes all entries from the data store.</summary>
        public void ClearData() => _data.Clear();
    }
}