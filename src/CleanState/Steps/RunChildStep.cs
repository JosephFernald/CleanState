// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;

namespace CleanState.Steps
{
    /// <summary>
    /// Spawns a child machine from a compiled definition and blocks until it completes.
    /// On first execution the child is created and started. Subsequent executions poll the
    /// child's status. When the child completes, the step continues. If the child faults,
    /// the parent faults with a descriptive exception.
    /// Stores the child <see cref="MachineId"/> in <see cref="MachineContext"/> so multiple
    /// parent instances sharing the same definition each track their own child.
    /// </summary>
    public sealed class RunChildStep : IStep
    {
        private readonly MachineDefinition _childDefinition;
        private readonly Action<MachineContext> _childInit;
        private readonly string _contextKey;

        /// <inheritdoc />
        public StepDebugInfo DebugInfo { get; }

        /// <summary>The compiled definition used to spawn the child machine.</summary>
        public MachineDefinition ChildDefinition => _childDefinition;

        /// <summary>Creates a RunChildStep that will spawn and wait for a child machine.</summary>
        /// <param name="childDefinition">The compiled definition for the child machine.</param>
        /// <param name="debugInfo">Debug metadata for this step.</param>
        /// <param name="childInit">Optional callback to initialize the child's context before it starts.</param>
        public RunChildStep(MachineDefinition childDefinition, StepDebugInfo debugInfo, Action<MachineContext> childInit = null)
        {
            _childDefinition = childDefinition ?? throw new ArgumentNullException(nameof(childDefinition));
            DebugInfo = debugInfo ?? throw new ArgumentNullException(nameof(debugInfo));
            _childInit = childInit;
            _contextKey = $"__child_{debugInfo.StateName}_{debugInfo.StepIndex}";
        }

        /// <inheritdoc />
        public StepResult Execute(MachineContext context)
        {
            var scheduler = context.Scheduler;
            if (scheduler == null)
                throw new InvalidOperationException(
                    "RunChildStep requires a Scheduler. Machines must be created via Scheduler.CreateMachine().");

            if (!context.TryGet<int>(_contextKey, out var childIdValue))
            {
                // First execution — spawn and start the child
                var child = scheduler.CreateMachine(_childDefinition);
                childIdValue = child.Id.Value;
                context.Set(_contextKey, childIdValue);

                _childInit?.Invoke(child.Context);
                child.Start(context.CurrentTime);

                // Child may have completed synchronously (all non-blocking steps)
                if (child.Status == MachineStatus.Completed)
                {
                    Cleanup(context, scheduler, childIdValue);
                    return StepResult.Continue();
                }

                if (child.Status == MachineStatus.Faulted)
                {
                    Cleanup(context, scheduler, childIdValue);
                    throw new InvalidOperationException(
                        $"Child machine '{_childDefinition.Name}' faulted during start.");
                }

                return StepResult.WaitForChild();
            }

            // Subsequent execution — check child status
            var childMachine = scheduler.GetMachine(new MachineId(childIdValue));

            if (childMachine == null)
            {
                // Child was removed externally
                context.Remove(_contextKey);
                return StepResult.Continue();
            }

            switch (childMachine.Status)
            {
                case MachineStatus.Completed:
                    Cleanup(context, scheduler, childIdValue);
                    return StepResult.Continue();

                case MachineStatus.Faulted:
                    Cleanup(context, scheduler, childIdValue);
                    throw new InvalidOperationException(
                        $"Child machine '{_childDefinition.Name}' faulted.");

                default:
                    return StepResult.WaitForChild();
            }
        }

        private void Cleanup(MachineContext context, Scheduler scheduler, int childIdValue)
        {
            context.Remove(_contextKey);
            scheduler.RemoveMachine(new MachineId(childIdValue));
        }
    }
}
