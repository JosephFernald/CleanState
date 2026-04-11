// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;

namespace CleanState.Composition
{
    /// <summary>
    /// Coordinates multiple machines as orthogonal state regions.
    /// Each region is an independent machine with its own lifecycle,
    /// but all share one scheduler and can observe each other's state.
    ///
    /// The aggregate state is the tuple of all region states:
    ///   { Locomotion: Running, Posture: Crouched, Weapon: Aiming }
    ///
    /// Regions can read each other's current state via well-known context keys
    /// (written by the coordinator after each update), enabling cross-region
    /// decisions without direct coupling.
    ///
    /// This preserves all core guarantees per machine:
    ///   - Single active state
    ///   - Deterministic step ordering
    ///   - Transition provenance
    ///   - Run-until-blocked execution
    ///   - Recovery snapshots
    /// </summary>
    public sealed class CompositeStateMachine
    {
        private readonly string _name;
        private readonly Scheduler _scheduler;
        private readonly List<RegionEntry> _regions = new List<RegionEntry>();
        private readonly Dictionary<string, RegionEntry> _regionsByName = new Dictionary<string, RegionEntry>();
        private readonly List<Action<CompositeStateMachine, float>> _constraints = new List<Action<CompositeStateMachine, float>>();
        private bool _started;

        /// <summary>
        /// Name of this composite machine (e.g., "PlayerState").
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// The shared scheduler driving all regions.
        /// </summary>
        public Scheduler Scheduler => _scheduler;

        /// <summary>
        /// Number of registered regions.
        /// </summary>
        public int RegionCount => _regions.Count;

        /// <summary>
        /// Whether all regions have completed.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                for (int i = 0; i < _regions.Count; i++)
                {
                    if (_regions[i].Machine.Status != MachineStatus.Completed)
                        return false;
                }
                return _regions.Count > 0;
            }
        }

        /// <summary>
        /// Whether any region has faulted.
        /// </summary>
        public bool IsFaulted
        {
            get
            {
                for (int i = 0; i < _regions.Count; i++)
                {
                    if (_regions[i].Machine.Status == MachineStatus.Faulted)
                        return true;
                }
                return false;
            }
        }

        public CompositeStateMachine(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _scheduler = new Scheduler();
        }

        public CompositeStateMachine(string name, Scheduler scheduler)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// Add a named region backed by a machine definition.
        /// </summary>
        public Machine AddRegion(string regionName, MachineDefinition definition, TraceBuffer traceBuffer = null)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentException("Region name cannot be null or empty.", nameof(regionName));
            if (_regionsByName.ContainsKey(regionName))
                throw new InvalidOperationException($"Region '{regionName}' already exists in composite '{_name}'.");
            if (_started)
                throw new InvalidOperationException("Cannot add regions after the composite machine has started.");

            var machine = _scheduler.CreateMachine(definition, traceBuffer);
            var entry = new RegionEntry(regionName, machine, definition);
            _regions.Add(entry);
            _regionsByName[regionName] = entry;
            return machine;
        }

        /// <summary>
        /// Add a constraint that runs after each update.
        /// Use this for cross-region rules (e.g., "cannot aim while reloading").
        /// The callback receives this composite and the current time.
        /// </summary>
        public void AddConstraint(Action<CompositeStateMachine, float> constraint)
        {
            _constraints.Add(constraint ?? throw new ArgumentNullException(nameof(constraint)));
        }

        /// <summary>
        /// Start all regions.
        /// </summary>
        public void Start(float currentTime)
        {
            if (_started)
                throw new InvalidOperationException($"Composite '{_name}' has already been started.");
            if (_regions.Count == 0)
                throw new InvalidOperationException($"Composite '{_name}' has no regions.");

            _started = true;

            for (int i = 0; i < _regions.Count; i++)
            {
                _regions[i].Machine.Start(currentTime);
            }

            SyncRegionStates();
        }

        /// <summary>
        /// Tick all regions. After the scheduler update, syncs cross-region state
        /// and evaluates constraints.
        /// </summary>
        public void Update(float currentTime)
        {
            _scheduler.Update(currentTime);
            SyncRegionStates();
            EvaluateConstraints(currentTime);
        }

        /// <summary>
        /// Send an event to a specific region by name.
        /// </summary>
        public void SendEvent(string regionName, EventId eventId, float currentTime)
        {
            var entry = GetRegionEntry(regionName);
            entry.Machine.SendEvent(eventId, currentTime);
            SyncRegionStates();
        }

        /// <summary>
        /// Send an event to a specific region by name, looking up the event by string.
        /// </summary>
        public void SendEvent(string regionName, string eventName, float currentTime)
        {
            var entry = GetRegionEntry(regionName);
            var eventId = MachineBuilder.EventIdFrom(entry.Definition, eventName);
            entry.Machine.SendEvent(eventId, currentTime);
            SyncRegionStates();
        }

        /// <summary>
        /// Broadcast an event to all regions. Each region ignores events it doesn't recognize.
        /// </summary>
        public void BroadcastEvent(string eventName, float currentTime)
        {
            for (int i = 0; i < _regions.Count; i++)
            {
                var entry = _regions[i];
                try
                {
                    var eventId = MachineBuilder.EventIdFrom(entry.Definition, eventName);
                    entry.Machine.SendEvent(eventId, currentTime);
                }
                catch (ArgumentException)
                {
                    // Region doesn't have this event — skip
                }
            }
            SyncRegionStates();
        }

        /// <summary>
        /// Get the current state name of a region.
        /// </summary>
        public string GetRegionState(string regionName)
        {
            var entry = GetRegionEntry(regionName);
            var machine = entry.Machine;
            if (machine.CurrentState.IsValid)
                return entry.Definition.NameLookup.GetStateName(machine.CurrentState);
            return "(none)";
        }

        /// <summary>
        /// Get the Machine instance for a specific region.
        /// </summary>
        public Machine GetRegionMachine(string regionName)
        {
            return GetRegionEntry(regionName).Machine;
        }

        /// <summary>
        /// Get the MachineDefinition for a specific region.
        /// </summary>
        public MachineDefinition GetRegionDefinition(string regionName)
        {
            return GetRegionEntry(regionName).Definition;
        }

        /// <summary>
        /// Get an aggregate snapshot of all regions.
        /// </summary>
        public CompositeSnapshot GetSnapshot()
        {
            var states = new Dictionary<string, RegionState>(_regions.Count);
            for (int i = 0; i < _regions.Count; i++)
            {
                var entry = _regions[i];
                var machine = entry.Machine;
                var stateName = machine.CurrentState.IsValid
                    ? entry.Definition.NameLookup.GetStateName(machine.CurrentState)
                    : "(none)";

                states[entry.Name] = new RegionState(
                    entry.Name,
                    stateName,
                    machine.Status,
                    machine.BlockReason);
            }
            return new CompositeSnapshot(_name, states);
        }

        /// <summary>
        /// Get all region names.
        /// </summary>
        public IEnumerable<string> RegionNames
        {
            get
            {
                for (int i = 0; i < _regions.Count; i++)
                    yield return _regions[i].Name;
            }
        }

        /// <summary>
        /// After each update, write every region's current state name into every
        /// other region's MachineContext under the key "__region.{name}".
        /// This lets steps in any region read the aggregate state without coupling.
        /// </summary>
        private void SyncRegionStates()
        {
            // First, collect current state names
            for (int i = 0; i < _regions.Count; i++)
            {
                var entry = _regions[i];
                var machine = entry.Machine;
                entry.CachedStateName = machine.CurrentState.IsValid
                    ? entry.Definition.NameLookup.GetStateName(machine.CurrentState)
                    : "(none)";
            }

            // Then write them into each machine's context
            for (int i = 0; i < _regions.Count; i++)
            {
                var ctx = _regions[i].Machine.Context;
                for (int j = 0; j < _regions.Count; j++)
                {
                    if (i == j) continue;
                    var other = _regions[j];
                    ctx.Set($"__region.{other.Name}", other.CachedStateName);
                }
            }
        }

        private void EvaluateConstraints(float currentTime)
        {
            for (int i = 0; i < _constraints.Count; i++)
            {
                _constraints[i](this, currentTime);
            }
        }

        private RegionEntry GetRegionEntry(string regionName)
        {
            if (!_regionsByName.TryGetValue(regionName, out var entry))
                throw new ArgumentException($"Region '{regionName}' not found in composite '{_name}'.");
            return entry;
        }

        private sealed class RegionEntry
        {
            public readonly string Name;
            public readonly Machine Machine;
            public readonly MachineDefinition Definition;
            public string CachedStateName;

            public RegionEntry(string name, Machine machine, MachineDefinition definition)
            {
                Name = name;
                Machine = machine;
                Definition = definition;
            }
        }
    }
}
