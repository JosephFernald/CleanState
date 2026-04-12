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
        public string MachineName { get; }
        public string StateName { get; }
        public int StepIndex { get; }
        public string StepType { get; }
        public string Label { get; }
        public string SourceFile { get; }
        public int SourceLine { get; }

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

        public override string ToString()
        {
            var location = SourceFile != null ? $" at {SourceFile}:{SourceLine}" : "";
            return $"[{MachineName}/{StateName}] Step {StepIndex} ({StepType}) \"{Label}\"{location}";
        }
    }
}