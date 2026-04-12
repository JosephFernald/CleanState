// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Composition
{
    /// <summary>
    /// A single region's state within a composite machine.
    /// </summary>
    public sealed class RegionState
    {
        /// <summary>Name of the region.</summary>
        public string RegionName { get; }
        /// <summary>Current state name within this region.</summary>
        public string StateName { get; }
        /// <summary>Current execution status of the region's machine.</summary>
        public MachineStatus Status { get; }
        /// <summary>The kind of block keeping the region's machine from progressing, if any.</summary>
        public BlockKind BlockReason { get; }

        /// <summary>Creates a new region state snapshot.</summary>
        public RegionState(string regionName, string stateName, MachineStatus status, BlockKind blockReason)
        {
            RegionName = regionName;
            StateName = stateName;
            Status = status;
            BlockReason = blockReason;
        }

        public override string ToString() => $"{RegionName}: {StateName}";
    }

    /// <summary>
    /// Aggregate snapshot of all regions in a composite machine.
    /// Represents the full state tuple at a point in time.
    /// </summary>
    public sealed class CompositeSnapshot
    {
        /// <summary>Name of the composite machine this snapshot was taken from.</summary>
        public string Name { get; }

        private readonly Dictionary<string, RegionState> _regions;

        /// <summary>Creates a new composite snapshot with the given region states.</summary>
        public CompositeSnapshot(string name, Dictionary<string, RegionState> regions)
        {
            Name = name;
            _regions = regions;
        }

        /// <summary>
        /// Get the state of a specific region by name.
        /// </summary>
        public RegionState GetRegion(string regionName)
        {
            return _regions[regionName];
        }

        /// <summary>
        /// Get the current state name for a region.
        /// </summary>
        public string GetRegionState(string regionName)
        {
            return _regions[regionName].StateName;
        }

        /// <summary>
        /// Get all region states.
        /// </summary>
        public IEnumerable<RegionState> Regions => _regions.Values;

        /// <summary>
        /// Number of regions.
        /// </summary>
        public int RegionCount => _regions.Count;
    }
}
