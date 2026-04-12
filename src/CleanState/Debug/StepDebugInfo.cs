// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace CleanState.Debug
{
    /// <summary>
    /// Debug metadata attached to each step at build time.
    /// Provides full traceability without runtime overhead.
    /// </summary>
    public sealed class StepDebugInfo
    {
        /// <summary>Name of the machine this step belongs to.</summary>
        public string MachineName { get; }

        /// <summary>Name of the state containing this step.</summary>
        public string StateName { get; }

        /// <summary>Zero-based index of this step within its state.</summary>
        public int StepIndex { get; }

        /// <summary>Type name of the step (e.g. "ActionStep", "DecisionStep").</summary>
        public string StepType { get; }

        /// <summary>Human-readable label for the step.</summary>
        public string Label { get; }

        /// <summary>Source file where the step was defined, if available.</summary>
        public string SourceFile { get; }

        /// <summary>Source line number where the step was defined.</summary>
        public int SourceLine { get; }

        /// <summary>Creates step debug metadata with the specified values.</summary>
        public StepDebugInfo(
            string machineName,
            string stateName,
            int stepIndex,
            string stepType,
            string label,
            string sourceFile = null,
            int sourceLine = 0)
        {
            MachineName = machineName;
            StateName = stateName;
            StepIndex = stepIndex;
            StepType = stepType;
            Label = label;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
        }

        /// <summary>Returns a formatted string identifying this step and its source location.</summary>
        public override string ToString()
        {
            var location = SourceFile != null ? $" at {SourceFile}:{SourceLine}" : "";
            return $"[{MachineName}/{StateName}] Step {StepIndex} ({StepType}) \"{Label}\"{location}";
        }
    }
}