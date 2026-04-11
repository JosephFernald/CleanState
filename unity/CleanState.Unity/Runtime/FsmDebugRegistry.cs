// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CleanState.Debug;
using CleanState.Runtime;

namespace CleanState.Unity.Runtime
{
    /// <summary>
    /// Global registry that tracks machines available for editor inspection.
    ///
    /// IMPORTANT: The registry exposes IFsmObservable (read-only) and TraceBuffer
    /// for observation. It does NOT expose raw Machine references.
    /// Debug commands go through FsmDebugController, which is optional and explicit.
    /// </summary>
    public static class FsmDebugRegistry
    {
        private static readonly List<TrackedMachine> _tracked = new List<TrackedMachine>();
        private static int _version;

        public static int Version => _version;

        public struct TrackedMachine
        {
            public IFsmObservable Observable;
            public TraceBuffer TraceBuffer;
            public FsmDebugController DebugController;
        }

        /// <summary>
        /// Register a machine for editor observation.
        /// Only the IFsmObservable surface is exposed to consumers.
        /// </summary>
        public static void Register(IFsmObservable observable, TraceBuffer traceBuffer = null, FsmDebugController debugController = null)
        {
            // Avoid duplicates
            for (int i = 0; i < _tracked.Count; i++)
            {
                if (_tracked[i].Observable == observable)
                    return;
            }

            _tracked.Add(new TrackedMachine
            {
                Observable = observable,
                TraceBuffer = traceBuffer,
                DebugController = debugController
            });
            _version++;
        }

        public static void Unregister(IFsmObservable observable)
        {
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i].Observable == observable)
                {
                    _tracked.RemoveAt(i);
                    _version++;
                    return;
                }
            }
        }

        public static int Count => _tracked.Count;

        public static TrackedMachine Get(int index) => _tracked[index];

        public static void Clear()
        {
            _tracked.Clear();
            _version++;
        }
    }
}