// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Runtime;
using CleanState.Debug;

namespace CleanState.Tests
{
    [TestFixture]
    public class MachineExecutionTests
    {
        [Test]
        public void Machine_RunsActionSteps_ToCompletion()
        {
            int counter = 0;
            var definition = new MachineBuilder("Counter")
                .State("Run")
                    .Then(ctx => counter++, "Increment1")
                    .Then(ctx => counter++, "Increment2")
                    .Then(ctx => counter++, "Increment3")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(counter, Is.EqualTo(3));
        }

        [Test]
        public void Machine_TransitionsBetweenStates()
        {
            var visited = new System.Collections.Generic.List<string>();
            var definition = new MachineBuilder("Flow")
                .State("A")
                    .Then(ctx => visited.Add("A"), "VisitA")
                    .GoTo("B", "GoToB")
                .State("B")
                    .Then(ctx => visited.Add("B"), "VisitB")
                    .GoTo("C", "GoToC")
                .State("C")
                    .Then(ctx => visited.Add("C"), "VisitC")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(visited, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void Machine_BlocksOnWaitForEvent_ResumesOnEvent()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("EventWait")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                    .Then(ctx => afterWait = true, "AfterGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(afterWait, Is.False);

            // Send the event
            var goEventId = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(goEventId, 1f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void Machine_BlocksOnWaitForTime_ResumesAfterDuration()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("TimeWait")
                .State("Wait")
                    .WaitForTime(2.0f, "Wait2Sec")
                    .Then(ctx => afterWait = true, "AfterWait")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Not enough time
            scheduler.Update(1.0f);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Enough time
            scheduler.Update(2.0f);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void Machine_BlocksOnPredicate_ResumesWhenTrue()
        {
            bool flag = false;
            bool afterWait = false;
            var definition = new MachineBuilder("PredicateWait")
                .State("Wait")
                    .WaitUntil(ctx => flag, "WaitForFlag")
                    .Then(ctx => afterWait = true, "AfterFlag")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            scheduler.Update(1f);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            flag = true;
            scheduler.Update(2f);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void Machine_Decision_TakesTrueBranch()
        {
            string result = null;
            var definition = new MachineBuilder("Decision")
                .State("Check")
                    .Then(ctx => ctx.Set("val", 10), "SetVal")
                    .Decision("Branch")
                        .When(ctx => ctx.Get<int>("val") > 5, "High", "IsHigh")
                        .Otherwise("Low", "IsLow")
                .State("High")
                    .Then(ctx => result = "high", "SetHigh")
                .State("Low")
                    .Then(ctx => result = "low", "SetLow")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(result, Is.EqualTo("high"));
        }

        [Test]
        public void Machine_Decision_TakesOtherwiseBranch()
        {
            string result = null;
            var definition = new MachineBuilder("Decision")
                .State("Check")
                    .Then(ctx => ctx.Set("val", 2), "SetVal")
                    .Decision("Branch")
                        .When(ctx => ctx.Get<int>("val") > 5, "High", "IsHigh")
                        .Otherwise("Low", "IsLow")
                .State("High")
                    .Then(ctx => result = "high", "SetHigh")
                .State("Low")
                    .Then(ctx => result = "low", "SetLow")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(result, Is.EqualTo("low"));
        }

        [Test]
        public void Machine_DebugSnapshot_ReflectsCurrentState()
        {
            var definition = new MachineBuilder("Debuggable")
                .State("WaitState")
                    .WaitForEvent("Signal", "WaitForSignal")
                .Build();

            var traceBuffer = new TraceBuffer();
            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition, traceBuffer);
            machine.Start(0f);

            var snapshot = machine.GetDebugSnapshot();
            Assert.That(snapshot.MachineName, Is.EqualTo("Debuggable"));
            Assert.That(snapshot.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(snapshot.CurrentStateName, Is.EqualTo("WaitState"));
            Assert.That(snapshot.BlockReason, Is.EqualTo(Steps.BlockKind.WaitForEvent));
        }

        [Test]
        public void Machine_TransitionTrace_IsRecorded()
        {
            var traceBuffer = new TraceBuffer();
            var definition = new MachineBuilder("Traced")
                .State("A")
                    .GoTo("B", "AtoB")
                .State("B")
                    .GoTo("C", "BtoC")
                .State("C")
                    .Then(ctx => { }, "Done")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition, traceBuffer);
            machine.Start(0f);

            var traces = traceBuffer.GetTraces();
            Assert.That(traces.Length, Is.EqualTo(2));
            Assert.That(traces[0].Detail, Is.EqualTo("AtoB"));
            Assert.That(traces[1].Detail, Is.EqualTo("BtoC"));
        }

        [Test]
        public void Machine_FaultsOnStepException()
        {
            var definition = new MachineBuilder("Faulty")
                .State("Boom")
                    .Then(ctx => throw new System.Exception("test error"), "ThrowStep")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);

            Assert.Throws<FsmExecutionException>(() => machine.Start(0f));
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Faulted));
        }
    }
}