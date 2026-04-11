// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CleanState.Identity;

namespace CleanState.Runtime
{
    /// <summary>
    /// Queued event with metadata for delivery to machines.
    /// </summary>
    public readonly struct QueuedEvent
    {
        public readonly EventId EventId;
        public readonly MachineId TargetMachine;

        /// <summary>
        /// If TargetMachine is Invalid, the event is broadcast to all machines.
        /// </summary>
        public QueuedEvent(EventId eventId, MachineId targetMachine)
        {
            EventId = eventId;
            TargetMachine = targetMachine;
        }

        public QueuedEvent(EventId eventId) : this(eventId, MachineId.Invalid) { }
    }

    /// <summary>
    /// Collects events during a frame and delivers them to machines via the scheduler.
    /// Double-buffered to avoid mutation during iteration.
    /// </summary>
    public sealed class EventQueue
    {
        private List<QueuedEvent> _pending = new List<QueuedEvent>();
        private List<QueuedEvent> _delivering = new List<QueuedEvent>();

        public int PendingCount => _pending.Count;

        public void Enqueue(EventId eventId)
        {
            _pending.Add(new QueuedEvent(eventId));
        }

        public void Enqueue(EventId eventId, MachineId target)
        {
            _pending.Add(new QueuedEvent(eventId, target));
        }

        /// <summary>
        /// Swap buffers and return events to deliver this frame.
        /// </summary>
        internal List<QueuedEvent> FlushAndSwap()
        {
            var temp = _delivering;
            _delivering = _pending;
            _pending = temp;
            _pending.Clear();
            return _delivering;
        }
    }
}