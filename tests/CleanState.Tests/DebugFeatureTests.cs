// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    /// <summary>
    /// Tests for the 5 debugger features:
    ///   1. Live active state highlighting (enriched snapshot)
    ///   2. Transition reason overlay (enriched transition data)
    ///   3. Step-level visibility (step type + label in snapshot)
    ///   4. Timeline / trace playback (trace buffer extraction)
    ///   5. Breakpoints (state entry, step, transition reason)
    /// </summary>
    [TestFixture]
    public class DebugFeatureTests
    {
        // =====================================================================
        // Feature 1: Live Active State — Enriched Snapshot
        // =====================================================================

        [Test]
        public void Snapshot_WhenBlockedOnEvent_ShowsEventName()
        {
            var definition = new MachineBuilder("EventBlock")
                .State("Wait")
                    .WaitForEvent("PlayerReady", "WaitForPlayer")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(0f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.BlockReason, Is.EqualTo(BlockKind.WaitForEvent));
            Assert.That(snap.WaitingForEventName, Is.EqualTo("PlayerReady"));
            Assert.That(snap.WaitingForEvent.IsValid, Is.True);
        }

        [Test]
        public void Snapshot_WhenBlockedOnTime_ShowsTargetTime()
        {
            var definition = new MachineBuilder("TimeBlock")
                .State("Wait")
                    .WaitForTime(5.0f, "Wait5Sec")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(10f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.BlockReason, Is.EqualTo(BlockKind.WaitForTime));
            Assert.That(snap.WaitUntilTime, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void Snapshot_ShowsStepCountInCurrentState()
        {
            var definition = new MachineBuilder("Steps")
                .State("Multi")
                    .Then(ctx => { }, "Step1")
                    .Then(ctx => { }, "Step2")
                    .Then(ctx => { }, "Step3")
                    .WaitForEvent("Go", "WaitGo")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(0f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.StepCountInCurrentState, Is.EqualTo(4));
        }

        // =====================================================================
        // Feature 2: Transition Reason — Provenance in Traces
        // =====================================================================

        [Test]
        public void TransitionTrace_CarriesReasonKind_Direct()
        {
            var buffer = new TraceBuffer();
            var definition = new MachineBuilder("Reason")
                .State("A")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => { }, "Done")
                .Build();

            var machine = new Scheduler().CreateMachine(definition, buffer);
            machine.Start(0f);

            var traces = buffer.GetTraces();
            Assert.That(traces.Length, Is.EqualTo(1));
            Assert.That(traces[0].Reason, Is.EqualTo(TransitionReasonKind.Direct));
            Assert.That(traces[0].Detail, Is.EqualTo("AtoB"));
        }

        [Test]
        public void TransitionTrace_CarriesReasonKind_DecisionBranch()
        {
            var buffer = new TraceBuffer();
            var definition = new MachineBuilder("DecisionTrace")
                .State("Check")
                    .Then(ctx => ctx.Set("v", 10), "SetV")
                    .Decision("Branch")
                        .When(ctx => ctx.Get<int>("v") > 5, "High", "IsHigh")
                        .Otherwise("Low", "IsLow")
                .State("High")
                    .Then(ctx => { }, "InHigh")
                .State("Low")
                    .Then(ctx => { }, "InLow")
                .Build();

            var machine = new Scheduler().CreateMachine(definition, buffer);
            machine.Start(0f);

            var traces = buffer.GetTraces();
            Assert.That(traces.Length, Is.EqualTo(1));
            Assert.That(traces[0].Reason, Is.EqualTo(TransitionReasonKind.DecisionBranch));
        }

        [Test]
        public void TransitionTrace_CarriesTimestamp()
        {
            var buffer = new TraceBuffer();
            var definition = new MachineBuilder("Timed")
                .State("A")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => { }, "Done")
                .Build();

            var machine = new Scheduler().CreateMachine(definition, buffer);
            machine.Start(42.5f);

            var traces = buffer.GetTraces();
            Assert.That(traces[0].Timestamp, Is.EqualTo(42.5f).Within(0.01f));
        }

        // =====================================================================
        // Feature 3: Step-Level Visibility — Type + Label in Snapshot
        // =====================================================================

        [Test]
        public void Snapshot_ShowsCurrentStepLabel()
        {
            var definition = new MachineBuilder("StepLabel")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(0f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.CurrentStepLabel, Is.EqualTo("WaitForGo"));
        }

        [Test]
        public void Snapshot_ShowsCurrentStepType()
        {
            var definition = new MachineBuilder("StepType")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(0f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.CurrentStepType, Is.EqualTo("WaitForEvent"));
        }

        [Test]
        public void Snapshot_StepInfoIsNull_WhenCompleted()
        {
            var definition = new MachineBuilder("Done")
                .State("Only")
                    .Then(ctx => { }, "Noop")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            var snap = machine.GetDebugSnapshot();
            // Step index is past the end, so no current step info
            Assert.That(snap.CurrentStepLabel, Is.Null);
        }

        // =====================================================================
        // Feature 4: Timeline — Trace Buffer Extraction
        // =====================================================================

        [Test]
        public void TraceBuffer_RecordsMultipleTransitions()
        {
            var buffer = new TraceBuffer();
            var definition = new MachineBuilder("Multi")
                .State("A").GoTo("B", "AtoB")
                .State("B").GoTo("C", "BtoC")
                .State("C").GoTo("D", "CtoD")
                .State("D").Then(ctx => { }, "Done")
                .Build();

            var machine = new Scheduler().CreateMachine(definition, buffer);
            machine.Start(0f);

            var traces = buffer.GetTraces();
            Assert.That(traces.Length, Is.EqualTo(3));
            Assert.That(traces[0].Detail, Is.EqualTo("AtoB"));
            Assert.That(traces[1].Detail, Is.EqualTo("BtoC"));
            Assert.That(traces[2].Detail, Is.EqualTo("CtoD"));
        }

        [Test]
        public void TraceBuffer_IsRingBuffer_OverwritesOldest()
        {
            var buffer = new TraceBuffer(4);

            for (int i = 0; i < 6; i++)
            {
                buffer.Record(new TransitionTrace(
                    new StateId(0), new StateId(1), 0,
                    TransitionReasonKind.Direct, $"Trace{i}", i));
            }

            Assert.That(buffer.Count, Is.EqualTo(4));
            var traces = buffer.GetTraces();
            Assert.That(traces[0].Detail, Is.EqualTo("Trace2")); // oldest surviving
            Assert.That(traces[3].Detail, Is.EqualTo("Trace5")); // newest
        }

        [Test]
        public void Snapshot_LastTransition_IsAvailable()
        {
            var buffer = new TraceBuffer();
            var definition = new MachineBuilder("LastTrans")
                .State("A").GoTo("B", "AtoB")
                .State("B").Then(ctx => { }, "Done")
                .Build();

            var machine = new Scheduler().CreateMachine(definition, buffer);
            machine.Start(5f);

            var snap = machine.GetDebugSnapshot();
            Assert.That(snap.LastTransition, Is.Not.Null);
            Assert.That(snap.LastTransition.Detail, Is.EqualTo("AtoB"));
            Assert.That(snap.LastTransition.Reason, Is.EqualTo(TransitionReasonKind.Direct));
        }

        // =====================================================================
        // Feature 5: Breakpoints
        // =====================================================================

        [Test]
        public void Breakpoint_OnStateEntry_PausesMachine()
        {
            var definition = new MachineBuilder("BPTest")
                .State("A")
                    .Then(ctx => { }, "InA")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => { }, "InB")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            var stateB = definition.GetStateByIndex(1).Id;
            ctrl.AddBreakpoint(FsmBreakpoint.OnStateEntry(stateB));

            machine.Start(0f);

            // Machine should have paused at state B entry
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(machine.CurrentState, Is.EqualTo(stateB));
            Assert.That(ctrl.IsPaused, Is.True);
            Assert.That(ctrl.BreakpointHit, Is.True);
        }

        [Test]
        public void Breakpoint_OnStep_PausesAtCorrectStep()
        {
            var definition = new MachineBuilder("StepBP")
                .State("A")
                    .Then(ctx => { }, "Step0")
                    .Then(ctx => { }, "Step1")
                    .Then(ctx => { }, "Step2")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            var stateA = definition.GetStateByIndex(0).Id;
            ctrl.AddBreakpoint(FsmBreakpoint.OnStep(stateA, 2));

            machine.Start(0f);

            // Should pause before executing step 2
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(machine.CurrentStepIndex, Is.EqualTo(2));
            Assert.That(ctrl.BreakpointHit, Is.True);
        }

        [Test]
        public void Breakpoint_OnTransitionReason_PausesOnMatch()
        {
            var definition = new MachineBuilder("TransBP")
                .State("A")
                    .Then(ctx => ctx.Set("v", 1), "SetV")
                    .Decision("Branch")
                        .When(ctx => ctx.Get<int>("v") > 0, "B", "IsPositive")
                        .Otherwise("C", "IsFallback")
                .State("B")
                    .Then(ctx => { }, "InB")
                .State("C")
                    .Then(ctx => { }, "InC")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            ctrl.AddBreakpoint(FsmBreakpoint.OnTransitionReason(TransitionReasonKind.DecisionBranch));

            machine.Start(0f);

            // The transition fires, which triggers the transition-reason breakpoint
            // The machine should be paused
            Assert.That(ctrl.IsPaused, Is.True);
            Assert.That(ctrl.BreakpointHit, Is.True);
            Assert.That(ctrl.LastHitBreakpoint.Kind, Is.EqualTo(FsmBreakpointKind.TransitionReason));
        }

        [Test]
        public void Breakpoint_Disabled_DoesNotPause()
        {
            var definition = new MachineBuilder("DisabledBP")
                .State("A")
                    .Then(ctx => { }, "InA")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => { }, "InB")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            var stateB = definition.GetStateByIndex(1).Id;
            var bp = ctrl.AddBreakpoint(FsmBreakpoint.OnStateEntry(stateB));
            bp.Enabled = false;

            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(ctrl.IsPaused, Is.False);
        }

        [Test]
        public void Breakpoint_Remove_StopsBreaking()
        {
            var definition = new MachineBuilder("RemoveBP")
                .State("A")
                    .WaitForEvent("Go", "WaitGo")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            var stateA = definition.GetStateByIndex(0).Id;
            var bp = ctrl.AddBreakpoint(FsmBreakpoint.OnStateEntry(stateA));
            Assert.That(ctrl.BreakpointCount, Is.EqualTo(1));

            ctrl.RemoveBreakpoint(bp);
            Assert.That(ctrl.BreakpointCount, Is.EqualTo(0));
        }

        [Test]
        public void Breakpoint_Resume_ClearsBreakpointHit()
        {
            var definition = new MachineBuilder("ResumeBP")
                .State("A")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => { }, "InB")
                .Build();

            var machine = new Scheduler().CreateMachine(definition);
            var ctrl = new FsmDebugController(machine);

            var stateB = definition.GetStateByIndex(1).Id;
            ctrl.AddBreakpoint(FsmBreakpoint.OnStateEntry(stateB));

            machine.Start(0f);
            Assert.That(ctrl.BreakpointHit, Is.True);

            ctrl.Resume();
            Assert.That(ctrl.BreakpointHit, Is.False);
            Assert.That(ctrl.LastHitBreakpoint, Is.Null);
        }

        [Test]
        public void BeforeStep_Hook_DoesNotExistOnIFsmObservable()
        {
            // The BeforeStep hook is on Machine, not IFsmObservable.
            // This ensures the editor can't set breakpoints by casting.
            var members = typeof(IFsmObservable).GetMembers();
            foreach (var m in members)
            {
                Assert.That(m.Name, Is.Not.EqualTo("BeforeStep"),
                    "BeforeStep must not be on IFsmObservable — it's a mutation hook");
            }
        }
    }
}