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
        public float CurrentTime { get; internal set; }
        public EventId LastReceivedEvent { get; internal set; } = EventId.Invalid;
        public StateId CurrentState { get; internal set; }

        /// <summary>
        /// Shared blackboard for domain data. Keyed by string for flexibility,
        /// but lookups only happen in action steps, not hot-path scheduling.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<string, object> _data
            = new System.Collections.Generic.Dictionary<string, object>();

        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        public T Get<T>(string key)
        {
            return (T)_data[key];
        }

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

        public bool Has(string key) => _data.ContainsKey(key);

        public void Remove(string key) => _data.Remove(key);

        public void ClearData() => _data.Clear();
    }
}