// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    /// <summary>
    /// These tests enshrine the observation boundary requirements:
    ///
    /// ALLOWED:
    ///   - Observation (reading debug snapshots, graph topology)
    ///   - Querying debug state
    ///   - Explicit debug commands via FsmDebugController (pause, step, jump)
    ///
    /// FORBIDDEN:
    ///   - Forcing transitions without explicit debug API
    ///   - Modifying context directly via inspector/tooling
    ///   - Stepping logic from editor hooks
    ///   - Implicit coupling (core checking if editor is open)
    /// </summary>
    [TestFixture]
    public class ObservationBoundaryTests
    {
        // --- IFsmObservable enforces read-only surface ---

        [Test]
        public void IFsmObservable_HasNoMutationMethods()
        {
            var observableType = typeof(IFsmObservable);
            var methods = observableType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            var mutationNames = new[]
            {
                "Start", "SendEvent", "Update", "ForceState",
                "Set", "Remove", "ClearData"
            };

            foreach (var method in methods)
            {
                // Skip property getters
                if (method.IsSpecialName) continue;

                Assert.That(mutationNames, Does.Not.Contain(method.Name),
                    $"IFsmObservable must not expose mutation method '{method.Name}'");
            }
        }

        [Test]
        public void IFsmObservable_OnlyExposesReadOnlyProperties()
        {
            var observableType = typeof(IFsmObservable);
            var properties = observableType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                Assert.That(prop.CanWrite, Is.False,
                    $"IFsmObservable property '{prop.Name}' must not have a setter");
            }
        }

        [Test]
        public void IFsmObservable_DoesNotExposeContext()
        {
            var observableType = typeof(IFsmObservable);
            var members = observableType.GetMembers(BindingFlags.Public | BindingFlags.Instance);

            var contextMembers = members.Where(m =>
                m.Name == "Context" ||
                m.Name.Contains("MachineContext")).ToArray();

            Assert.That(contextMembers, Is.Empty,
                "IFsmObservable must not expose MachineContext — it allows direct data mutation");
        }

        [Test]
        public void IFsmObservable_DoesNotExposeForceState()
        {
            var observableType = typeof(IFsmObservable);
            var methods = observableType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(methods.Any(m => m.Name == "ForceState"), Is.False,
                "IFsmObservable must not expose ForceState — transitions must go through FsmDebugController");
        }

        [Test]
        public void IFsmObservable_DoesNotExposeSendEvent()
        {
            var observableType = typeof(IFsmObservable);
            var methods = observableType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(methods.Any(m => m.Name == "SendEvent"), Is.False,
                "IFsmObservable must not expose SendEvent — events must go through the scheduler");
        }

        [Test]
        public void Machine_ImplementsIFsmObservable()
        {
            Assert.That(typeof(IFsmObservable).IsAssignableFrom(typeof(Machine)),
                "Machine must implement IFsmObservable so it can be registered for observation");
        }

        // --- GetDebugSnapshot is pure observation ---

        [Test]
        public void GetDebugSnapshot_DoesNotMutateMachineState()
        {
            var definition = new MachineBuilder("Snapshot")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var statusBefore = machine.Status;
            var stateBefore = machine.CurrentState;
            var stepBefore = machine.CurrentStepIndex;

            // Call snapshot multiple times
            var snap1 = machine.GetDebugSnapshot();
            var snap2 = machine.GetDebugSnapshot();
            var snap3 = machine.GetDebugSnapshot();

            Assert.That(machine.Status, Is.EqualTo(statusBefore));
            Assert.That(machine.CurrentState, Is.EqualTo(stateBefore));
            Assert.That(machine.CurrentStepIndex, Is.EqualTo(stepBefore));
        }

        // --- FsmDebugController gates debug commands ---

        [Test]
        public void DebugController_StepOnce_RequiresPaused()
        {
            var definition = new MachineBuilder("Gated")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var controller = new FsmDebugController(machine);
            // NOT paused — StepOnce must throw
            Assert.Throws<InvalidOperationException>(() => controller.StepOnce(),
                "StepOnce must require the machine to be paused");
        }

        [Test]
        public void DebugController_JumpToState_RequiresPaused()
        {
            var definition = new MachineBuilder("Gated")
                .State("A")
                    .WaitForEvent("Go", "WaitForGo")
                .State("B")
                    .Then(ctx => { }, "Noop")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var controller = new FsmDebugController(machine);
            var stateB = definition.GetStateByIndex(1).Id;

            // NOT paused — JumpToState must throw
            Assert.Throws<InvalidOperationException>(() => controller.JumpToState(stateB),
                "JumpToState must require the machine to be paused");
        }

        [Test]
        public void DebugController_Pause_PreventsExecution()
        {
            var definition = new MachineBuilder("Pausable")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var controller = new FsmDebugController(machine);
            controller.Pause();

            Assert.That(controller.IsPaused, Is.True);
            Assert.That(controller.ShouldExecute(), Is.False,
                "A paused machine with no pending command should not execute");
        }

        [Test]
        public void DebugController_Resume_AllowsExecution()
        {
            var definition = new MachineBuilder("Resumable")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var controller = new FsmDebugController(machine);
            controller.Pause();
            Assert.That(controller.ShouldExecute(), Is.False);

            controller.Resume();
            Assert.That(controller.ShouldExecute(), Is.True);
        }

        // --- Core has no implicit editor coupling ---

        [Test]
        public void CoreAssembly_DoesNotReferenceUnity()
        {
            var coreAssembly = typeof(Machine).Assembly;
            var references = coreAssembly.GetReferencedAssemblies();

            foreach (var reference in references)
            {
                Assert.That(reference.Name, Does.Not.StartWith("UnityEngine"),
                    $"Core assembly must not reference {reference.Name}");
                Assert.That(reference.Name, Does.Not.StartWith("UnityEditor"),
                    $"Core assembly must not reference {reference.Name}");
            }
        }

        [Test]
        public void CoreAssembly_NoEditorOpenChecks()
        {
            // Verify no type in the core assembly checks for editor/view/window state.
            // This catches patterns like: if (SomeView.IsOpen) { ... }
            var coreAssembly = typeof(Machine).Assembly;
            var allTypes = coreAssembly.GetTypes();

            foreach (var type in allTypes)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Static | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    Assert.That(field.Name, Does.Not.Contain("IsOpen").IgnoreCase,
                        $"Core type {type.Name} has field '{field.Name}' suggesting implicit editor coupling");
                    Assert.That(field.Name, Does.Not.Contain("EditorWindow").IgnoreCase,
                        $"Core type {type.Name} has field '{field.Name}' suggesting implicit editor coupling");
                    Assert.That(field.Name, Does.Not.Contain("Inspector").IgnoreCase,
                        $"Core type {type.Name} has field '{field.Name}' suggesting implicit editor coupling");
                }
            }
        }

        // --- DebugSnapshot is a detached copy, not a live reference ---

        [Test]
        public void DebugSnapshot_IsDetachedCopy_NotLiveReference()
        {
            int counter = 0;
            var definition = new MachineBuilder("Detach")
                .State("A")
                    .Then(ctx => counter++, "Inc")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var snapshot = machine.GetDebugSnapshot();
            var stepAtSnapshot = snapshot.CurrentStepIndex;
            var statusAtSnapshot = snapshot.Status;

            // Advance the machine
            var goId = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(goId, 1f);

            // Snapshot should not have changed — it's a detached copy
            Assert.That(snapshot.CurrentStepIndex, Is.EqualTo(stepAtSnapshot),
                "DebugSnapshot must be a detached copy, not a live reference");
            Assert.That(snapshot.Status, Is.EqualTo(statusAtSnapshot),
                "DebugSnapshot.Status must not change after machine advances");
        }

        // --- TraceBuffer is read-only from observer perspective ---

        [Test]
        public void TraceBuffer_GetTraces_ReturnsNewArray()
        {
            var buffer = new TraceBuffer(16);
            var trace = new TransitionTrace(
                new CleanState.Identity.StateId(0),
                new CleanState.Identity.StateId(1),
                0, TransitionReasonKind.Direct, "test", 0f);

            buffer.Record(trace);

            var traces1 = buffer.GetTraces();
            var traces2 = buffer.GetTraces();

            // Each call returns a new array — modifying one doesn't affect the buffer
            Assert.That(traces1, Is.Not.SameAs(traces2),
                "GetTraces must return a new array each time, not the internal buffer");
        }

        // --- MachineDefinition is immutable after build ---

        [Test]
        public void MachineDefinition_HasNoPublicMutationMethods()
        {
            var defType = typeof(MachineDefinition);
            var methods = defType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                if (method.IsSpecialName) continue;

                // All public methods should be getters/queries
                Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(void)),
                    $"MachineDefinition.{method.Name}() returns void, suggesting mutation");
            }
        }
    }
}