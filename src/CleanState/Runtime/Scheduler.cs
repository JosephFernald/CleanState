// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CleanState.Debug;
using CleanState.Identity;

namespace CleanState.Runtime
{
    /// <summary>
    /// Drives machine execution each frame. Delivers events, checks time/predicate blocks,
    /// and runs machines that are ready.
    /// </summary>
    public sealed class Scheduler
    {
        private readonly List<Machine> _machines = new List<Machine>();
        private readonly Dictionary<int, Machine> _machineById = new Dictionary<int, Machine>();
        private readonly EventQueue _eventQueue;
        private int _nextMachineId;

        public EventQueue Events => _eventQueue;
        public int MachineCount => _machines.Count;

        public Scheduler() : this(new EventQueue()) { }

        public Scheduler(EventQueue eventQueue)
        {
            _eventQueue = eventQueue ?? throw new ArgumentNullException(nameof(eventQueue));
        }

        /// <summary>
        /// Create and register a machine from a definition.
        /// </summary>
        public Machine CreateMachine(MachineDefinition definition, TraceBuffer traceBuffer = null)
        {
            var id = new MachineId(_nextMachineId++);
            var machine = new Machine(id, definition, traceBuffer);
            _machines.Add(machine);
            _machineById[id.Value] = machine;
            return machine;
        }

        /// <summary>
        /// Remove a completed or faulted machine from the scheduler.
        /// </summary>
        public bool RemoveMachine(MachineId id)
        {
            if (_machineById.TryGetValue(id.Value, out var machine))
            {
                _machineById.Remove(id.Value);
                _machines.Remove(machine);
                return true;
            }
            return false;
        }

        public Machine GetMachine(MachineId id)
        {
            _machineById.TryGetValue(id.Value, out var machine);
            return machine;
        }

        /// <summary>
        /// Main update loop. Call once per frame with the current time.
        /// Delivers queued events, then ticks blocked machines.
        /// </summary>
        public void Update(float currentTime)
        {
            // 1. Deliver queued events
            var events = _eventQueue.FlushAndSwap();
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.TargetMachine.IsValid)
                {
                    if (_machineById.TryGetValue(evt.TargetMachine.Value, out var target))
                    {
                        target.SendEvent(evt.EventId, currentTime);
                    }
                }
                else
                {
                    // Broadcast to all machines
                    for (int j = 0; j < _machines.Count; j++)
                    {
                        _machines[j].SendEvent(evt.EventId, currentTime);
                    }
                }
            }

            // 2. Tick blocked machines (time/predicate waits)
            for (int i = 0; i < _machines.Count; i++)
            {
                var machine = _machines[i];
                if (machine.Status == MachineStatus.Blocked)
                {
                    machine.Update(currentTime);
                }
            }
        }
    }
}