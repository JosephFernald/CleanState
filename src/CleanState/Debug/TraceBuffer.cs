// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace CleanState.Debug
{
    /// <summary>
    /// Fixed-size ring buffer that stores recent transition traces.
    /// Enabled in debug builds for post-mortem analysis.
    /// </summary>
    public sealed class TraceBuffer
    {
        private readonly TransitionTrace[] _buffer;
        private int _head;
        private int _count;

        /// <summary>Creates a trace buffer with the specified capacity.</summary>
        public TraceBuffer(int capacity = 128)
        {
            _buffer = new TransitionTrace[capacity];
        }

        /// <summary>Number of traces currently stored.</summary>
        public int Count => _count;

        /// <summary>Maximum number of traces this buffer can hold.</summary>
        public int Capacity => _buffer.Length;

        /// <summary>Records a transition trace, overwriting the oldest entry if full.</summary>
        public void Record(TransitionTrace trace)
        {
            _buffer[_head] = trace;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        /// <summary>
        /// Returns traces in chronological order (oldest first).
        /// </summary>
        public TransitionTrace[] GetTraces()
        {
            var result = new TransitionTrace[_count];
            if (_count < _buffer.Length)
            {
                System.Array.Copy(_buffer, 0, result, 0, _count);
            }
            else
            {
                int oldest = _head;
                int firstChunk = _buffer.Length - oldest;
                System.Array.Copy(_buffer, oldest, result, 0, firstChunk);
                System.Array.Copy(_buffer, 0, result, firstChunk, oldest);
            }
            return result;
        }

        /// <summary>Removes all recorded traces.</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}