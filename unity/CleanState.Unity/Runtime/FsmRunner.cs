// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using UnityEngine;

namespace CleanState.Unity.Runtime
{
    /// <summary>
    /// Thin MonoBehaviour that drives the scheduler and registers machines
    /// with the debug registry for editor visualization.
    ///
    /// This is the only Unity-coupled runtime component.
    /// Drop it on a GameObject and call CreateAndStart() to get started.
    ///
    /// NOTE: The scheduler is intentionally NOT exposed publicly.
    /// All machine creation goes through this class so registration is guaranteed.
    /// </summary>
    public class FsmRunner : MonoBehaviour
    {
        [SerializeField] private bool _enableTracing = true;
        [SerializeField] private int _traceBufferSize = 128;
        [SerializeField] private bool _enableDebugCommands = false;

        private Scheduler _scheduler;

        protected virtual void Awake()
        {
            _scheduler = new Scheduler();
        }

        protected virtual void Update()
        {
            _scheduler.Update(Time.time);
        }

        /// <summary>
        /// Create a machine, start it, and register it for editor observation.
        /// Returns the Machine for the caller to use in gameplay code.
        /// The editor only sees an IFsmObservable — it cannot reach this Machine.
        /// </summary>
        public Machine CreateAndStart(MachineDefinition definition)
        {
            var traceBuffer = _enableTracing ? new TraceBuffer(_traceBufferSize) : null;
            var machine = _scheduler.CreateMachine(definition, traceBuffer);

            RegisterForObservation(machine, traceBuffer);
            machine.OnCompleted += OnMachineCompleted;

            machine.Start(Time.time);
            return machine;
        }

        /// <summary>
        /// Create a machine and register it, but don't start it yet.
        /// Call machine.Start(Time.time) when ready.
        /// </summary>
        public Machine Create(MachineDefinition definition)
        {
            var traceBuffer = _enableTracing ? new TraceBuffer(_traceBufferSize) : null;
            var machine = _scheduler.CreateMachine(definition, traceBuffer);

            RegisterForObservation(machine, traceBuffer);
            return machine;
        }

        /// <summary>
        /// Send an event through the scheduler's event queue (broadcast).
        /// Delivered on next Update.
        /// </summary>
        public void SendEvent(EventId eventId)
        {
            _scheduler.Events.Enqueue(eventId);
        }

        /// <summary>
        /// Send a targeted event to a specific machine.
        /// </summary>
        public void SendEvent(EventId eventId, MachineId target)
        {
            _scheduler.Events.Enqueue(eventId, target);
        }

        private void RegisterForObservation(Machine machine, TraceBuffer traceBuffer)
        {
            // Machine implements IFsmObservable — the registry only stores that interface.
            // The editor can never cast back to Machine because it doesn't reference
            // Machine as a concrete type.
            FsmDebugController debugController = null;
            if (_enableDebugCommands)
            {
                debugController = new FsmDebugController(machine);
            }

            FsmDebugRegistry.Register(machine, traceBuffer, debugController);
        }

        private void OnMachineCompleted(Machine machine)
        {
            machine.OnCompleted -= OnMachineCompleted;
        }

        protected virtual void OnDestroy()
        {
            FsmDebugRegistry.Clear();
        }
    }
}