// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    [TestFixture]
    public class ChildMachineTests
    {
        // ── Basic lifecycle ─────────────────────────────────────

        [Test]
        public void RunChild_SyncChild_CompletesImmediately()
        {
            int parentCounter = 0;
            int childCounter = 0;

            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => childCounter++, "ChildWork")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .Then(ctx => parentCounter++, "BeforeChild")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => parentCounter++, "AfterChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(parentCounter, Is.EqualTo(2));
            Assert.That(childCounter, Is.EqualTo(1));
        }

        [Test]
        public void RunChild_AsyncChild_BlocksUntilChildCompletes()
        {
            bool afterChild = false;

            var childDef = new MachineBuilder("Child")
                .State("Wait")
                    .WaitForTime(3.0, "ChildWait")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => afterChild = true, "AfterChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(machine.BlockReason, Is.EqualTo(BlockKind.WaitForChildMachine));
            Assert.That(afterChild, Is.False);

            // Not enough time for child
            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Child completes on this tick
            scheduler.Update(3.0);
            // Parent re-evaluates on next tick and sees child completed
            scheduler.Update(3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterChild, Is.True);
        }

        [Test]
        public void RunChild_ChildWithEvents_CompletesOnEvent()
        {
            bool afterChild = false;

            var childDef = new MachineBuilder("Child")
                .State("Wait")
                    .WaitForEvent("ChildGo", "WaitGo")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => afterChild = true, "AfterChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Send the child's event via broadcast
            var childGoEvent = MachineBuilder.EventIdFrom(childDef, "ChildGo");
            scheduler.Events.Enqueue(childGoEvent);
            scheduler.Update(1.0);

            // Parent should continue after child completes
            // The scheduler Update ticks all machines, including the parent
            // But the parent needs to be re-evaluated too
            scheduler.Update(1.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterChild, Is.True);
        }

        [Test]
        public void RunChild_ChildWithPredicate_CompletesWhenTrue()
        {
            bool flag = false;
            bool afterChild = false;

            var childDef = new MachineBuilder("Child")
                .State("Wait")
                    .WaitUntil(ctx => flag, "WaitFlag")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => afterChild = true, "AfterChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            scheduler.Update(1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            flag = true;
            scheduler.Update(2.0); // child unblocks and completes
            scheduler.Update(2.0); // parent re-evaluates and continues

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterChild, Is.True);
        }

        // ── Child initialization ────────────────────────────────

        [Test]
        public void RunChild_ChildInit_SetsContextBeforeStart()
        {
            string received = null;

            var childDef = new MachineBuilder("Child")
                .State("Read")
                    .Then(ctx => received = ctx.Get<string>("input"), "ReadInput")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild",
                        childInit: ctx => ctx.Set("input", "hello from parent"))
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(received, Is.EqualTo("hello from parent"));
        }

        // ── Fault propagation ───────────────────────────────────

        [Test]
        public void RunChild_ChildFaultsDuringStart_PropagatesException()
        {
            var childDef = new MachineBuilder("FaultyChild")
                .State("Boom")
                    .Then(ctx => throw new System.Exception("child error"), "Throw")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);

            // The child faults during Start, which happens inside RunChildStep.Execute,
            // which is called during parent's RunUntilBlocked. The child's FsmExecutionException
            // is caught by the child's Machine, setting it to Faulted. RunChildStep then
            // detects the fault and throws.
            Assert.Throws<FsmExecutionException>(() => machine.Start(0.0));
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Faulted));
        }

        [Test]
        public void RunChild_ChildFaultsDuringUpdate_PropagatesOnParentEval()
        {
            bool shouldFault = false;

            var childDef = new MachineBuilder("FaultyChild")
                .State("Wait")
                    .WaitUntil(ctx =>
                    {
                        if (shouldFault) throw new System.Exception("delayed fault");
                        return false;
                    }, "MaybeFault")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            shouldFault = true;

            // Child faults during scheduler update
            Assert.Throws<FsmExecutionException>(() => scheduler.Update(1.0));

            // Next parent tick detects the faulted child
            Assert.Throws<FsmExecutionException>(() => scheduler.Update(1.0));
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Faulted));
        }

        // ── Cleanup ─────────────────────────────────────────────

        [Test]
        public void RunChild_ChildRemovedFromSchedulerAfterCompletion()
        {
            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => { }, "Work")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);

            Assert.That(scheduler.MachineCount, Is.EqualTo(1)); // just parent

            machine.Start(0.0);

            // Child was created and removed during sync execution
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(scheduler.MachineCount, Is.EqualTo(1)); // only parent remains
        }

        [Test]
        public void RunChild_AsyncChild_CleanedUpAfterCompletion()
        {
            var childDef = new MachineBuilder("Child")
                .State("Wait")
                    .WaitForTime(1.0, "ChildWait")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(scheduler.MachineCount, Is.EqualTo(2)); // parent + child

            scheduler.Update(1.0); // child completes
            scheduler.Update(1.0); // parent re-evaluates, cleans up child

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(scheduler.MachineCount, Is.EqualTo(1)); // only parent
        }

        [Test]
        public void RunChild_ContextKeyClearedAfterCompletion()
        {
            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => { }, "Work")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(machine.Context.Has("__child_Main_1"), Is.False);
        }

        // ── Transition after child ──────────────────────────────

        [Test]
        public void RunChild_FollowedByTransition()
        {
            string result = null;

            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => { }, "Work")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                    .GoTo("Done", "GoToDone")
                .State("Done")
                    .Then(ctx => result = "done", "SetDone")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(result, Is.EqualTo("done"));
        }

        // ── Multi-state child ───────────────────────────────────

        [Test]
        public void RunChild_MultiStateChild_ExecutesFully()
        {
            var visited = new System.Collections.Generic.List<string>();

            var childDef = new MachineBuilder("Child")
                .State("A")
                    .Then(ctx => visited.Add("A"), "VisitA")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => visited.Add("B"), "VisitB")
                    .GoTo("C", "BtoC")
                .State("C")
                    .Then(ctx => visited.Add("C"), "VisitC")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(visited, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        // ── Nested children ─────────────────────────────────────

        [Test]
        public void RunChild_NestedChild_CompletesCorrectly()
        {
            int grandchildCounter = 0;
            int childCounter = 0;
            int parentCounter = 0;

            var grandchildDef = new MachineBuilder("Grandchild")
                .State("Do")
                    .Then(ctx => grandchildCounter++, "GrandchildWork")
                .Build();

            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => childCounter++, "ChildBefore")
                    .RunChild(grandchildDef, "RunGrandchild")
                    .Then(ctx => childCounter++, "ChildAfter")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .Then(ctx => parentCounter++, "ParentBefore")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => parentCounter++, "ParentAfter")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(grandchildCounter, Is.EqualTo(1));
            Assert.That(childCounter, Is.EqualTo(2));
            Assert.That(parentCounter, Is.EqualTo(2));
        }

        // ── Sequential children ─────────────────────────────────

        [Test]
        public void RunChild_TwoSequentialChildren()
        {
            int child1Count = 0;
            int child2Count = 0;

            var child1Def = new MachineBuilder("Child1")
                .State("Do")
                    .Then(ctx => child1Count++, "Child1Work")
                .Build();

            var child2Def = new MachineBuilder("Child2")
                .State("Do")
                    .Then(ctx => child2Count++, "Child2Work")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(child1Def, "RunChild1")
                    .RunChild(child2Def, "RunChild2")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(child1Count, Is.EqualTo(1));
            Assert.That(child2Count, Is.EqualTo(1));
        }

        // ── Builder validation ──────────────────────────────────

        [Test]
        public void RunChild_NullDefinition_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                new MachineBuilder("Bad")
                    .State("Main")
                        .RunChild(null, "NullChild")
                    .Build();
            });
        }

        // ── Child with composite waits ──────────────────────────

        [Test]
        public void RunChild_ChildWithWaitForAll()
        {
            bool flag = false;
            bool afterChild = false;

            var childDef = new MachineBuilder("Child")
                .State("Gate")
                    .WaitForAll(w => w
                        .Time(1.0)
                        .Predicate(ctx => flag), "ChildGate")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "RunChild")
                    .Then(ctx => afterChild = true, "AfterChild")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked)); // predicate still false

            flag = true;
            scheduler.Update(2.0); // child unblocks
            scheduler.Update(2.0); // parent re-evaluates

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterChild, Is.True);
        }

        // ── Definition reuse ────────────────────────────────────

        [Test]
        public void RunChild_SameDefinitionUsedTwice_IndependentInstances()
        {
            int counter = 0;

            var childDef = new MachineBuilder("Child")
                .State("Do")
                    .Then(ctx => counter++, "Increment")
                .Build();

            var parentDef = new MachineBuilder("Parent")
                .State("Main")
                    .RunChild(childDef, "First")
                    .RunChild(childDef, "Second")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(parentDef);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(counter, Is.EqualTo(2)); // each child ran independently
        }
    }
}
