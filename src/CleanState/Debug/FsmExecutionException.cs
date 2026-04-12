// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace CleanState.Debug
{
    /// <summary>
    /// Wraps exceptions thrown during step execution with full context.
    /// </summary>
    public sealed class FsmExecutionException : Exception
    {
        /// <summary>Debug metadata for the step that threw the exception.</summary>
        public StepDebugInfo StepInfo { get; }

        /// <summary>Creates an execution exception wrapping the inner exception with step context.</summary>
        public FsmExecutionException(StepDebugInfo stepInfo, Exception innerException)
            : base($"FSM execution failed at {stepInfo}", innerException)
        {
            StepInfo = stepInfo;
        }
    }
}