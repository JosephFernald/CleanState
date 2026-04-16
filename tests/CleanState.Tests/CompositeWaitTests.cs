// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    [TestFixture]
    public class CompositeWaitTests
    {
        // ── WaitForAll ──────────────────────────────────────────

        [Test]
        public void WaitForAll_BlocksUntilAllEventsReceived()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AllEvents")
                .State("Wait")
                    .WaitForAll(w => w.Event("A").Event("B"), "WaitBoth")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(machine.BlockReason, Is.EqualTo(BlockKind.WaitForComposite));

            // Send first event — still blocked
            var eventA = MachineBuilder.EventIdFrom(definition, "A");
            machine.SendEvent(eventA, 1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(afterWait, Is.False);

            // Send second event — unblocks
            var eventB = MachineBuilder.EventIdFrom(definition, "B");
            machine.SendEvent(eventB, 2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAll_BlocksUntilTimeAndEventBothSatisfied()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AllTimeEvent")
                .State("Wait")
                    .WaitForAll(w => w.Time(3.0).Event("Go"), "WaitTimeAndEvent")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Time passes but event not received — still blocked
            scheduler.Update(4.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Event arrives — both conditions now met
            var goEvent = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(goEvent, 4.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAll_BlocksUntilPredicateAndTimeBothSatisfied()
        {
            bool flag = false;
            bool afterWait = false;
            var definition = new MachineBuilder("AllPredTime")
                .State("Wait")
                    .WaitForAll(w => w.Time(2.0).Predicate(ctx => flag), "WaitPredAndTime")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Time elapsed but predicate false
            scheduler.Update(3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Predicate true and time already elapsed
            flag = true;
            scheduler.Update(3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAll_AllThreeConditionTypes()
        {
            bool flag = false;
            bool afterWait = false;
            var definition = new MachineBuilder("AllThree")
                .State("Wait")
                    .WaitForAll(w => w
                        .Event("Signal")
                        .Time(1.0)
                        .Predicate(ctx => flag), "WaitAll3")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Send event first
            var signal = MachineBuilder.EventIdFrom(definition, "Signal");
            machine.SendEvent(signal, 0.5);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Time elapses but predicate still false
            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Predicate becomes true — all conditions met
            flag = true;
            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        // ── WaitForAny ──────────────────────────────────────────

        [Test]
        public void WaitForAny_UnblocksOnFirstEvent()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AnyEvent")
                .State("Wait")
                    .WaitForAny(w => w.Event("A").Event("B"), "WaitEither")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(machine.BlockReason, Is.EqualTo(BlockKind.WaitForComposite));

            // Send just one event — unblocks
            var eventB = MachineBuilder.EventIdFrom(definition, "B");
            machine.SendEvent(eventB, 1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAny_UnblocksOnTime()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AnyTime")
                .State("Wait")
                    .WaitForAny(w => w.Event("Go").Time(5.0), "WaitEventOrTime")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Not enough time and no event
            scheduler.Update(3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Time elapses — unblocks without event
            scheduler.Update(5.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAny_UnblocksOnPredicate()
        {
            bool flag = false;
            bool afterWait = false;
            var definition = new MachineBuilder("AnyPred")
                .State("Wait")
                    .WaitForAny(w => w.Event("Go").Predicate(ctx => flag), "WaitEventOrPred")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            scheduler.Update(1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            flag = true;
            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAny_EventBeatsTime()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AnyEventBeatsTime")
                .State("Wait")
                    .WaitForAny(w => w.Event("Fast").Time(10.0), "WaitRace")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Event arrives early
            var fast = MachineBuilder.EventIdFrom(definition, "Fast");
            machine.SendEvent(fast, 1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        // ── Transitions after composite waits ───────────────────

        [Test]
        public void WaitForAll_FollowedByTransition()
        {
            string result = null;
            var definition = new MachineBuilder("AllThenGo")
                .State("Wait")
                    .WaitForAll(w => w.Event("A").Event("B"), "WaitBoth")
                    .GoTo("Done", "GoToDone")
                .State("Done")
                    .Then(ctx => result = "done", "SetDone")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            var a = MachineBuilder.EventIdFrom(definition, "A");
            var b = MachineBuilder.EventIdFrom(definition, "B");
            machine.SendEvent(a, 1.0);
            machine.SendEvent(b, 2.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(result, Is.EqualTo("done"));
        }

        // ── Edge cases ──────────────────────────────────────────

        [Test]
        public void WaitForAll_SingleCondition_BehavesLikeRegularWait()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AllSingle")
                .State("Wait")
                    .WaitForAll(w => w.Time(2.0), "WaitSingle")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            scheduler.Update(1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            scheduler.Update(2.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAny_SingleCondition_BehavesLikeRegularWait()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AnySingle")
                .State("Wait")
                    .WaitForAny(w => w.Event("Go"), "WaitSingle")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            var go = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(go, 1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAll_DuplicateEventNotSatisfiedTwice()
        {
            // Sending the same event twice should not satisfy two different event conditions
            bool afterWait = false;
            var definition = new MachineBuilder("AllDupeEvent")
                .State("Wait")
                    .WaitForAll(w => w.Event("X").Event("Y"), "WaitXY")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            var x = MachineBuilder.EventIdFrom(definition, "X");
            machine.SendEvent(x, 1.0);
            machine.SendEvent(x, 2.0); // duplicate of X, not Y
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(afterWait, Is.False);

            // Now send Y
            var y = MachineBuilder.EventIdFrom(definition, "Y");
            machine.SendEvent(y, 3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }

        [Test]
        public void WaitForAll_CleansUpContextKeys()
        {
            var definition = new MachineBuilder("AllCleanup")
                .State("Wait")
                    .WaitForAll(w => w.Time(1.0).Event("Go"), "WaitTimeEvent")
                    .Then(ctx =>
                    {
                        // After completion, internal keys should be cleaned up
                        // Verify no leftover keys with the internal prefix
                    }, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            scheduler.Update(2.0);
            var go = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(go, 2.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            // Verify no internal keys leaked by checking the context doesn't have them
            Assert.That(machine.Context.Has("__wfa_Wait_0_time_0"), Is.False);
        }

        [Test]
        public void WaitForAny_CleansUpTimeKeys()
        {
            var definition = new MachineBuilder("AnyCleanup")
                .State("Wait")
                    .WaitForAny(w => w.Time(1.0).Time(5.0), "WaitEitherTime")
                    .Then(ctx => { }, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // First time condition met
            scheduler.Update(1.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(machine.Context.Has("__wfany_Wait_0_time_0"), Is.False);
            Assert.That(machine.Context.Has("__wfany_Wait_0_time_1"), Is.False);
        }

        // ── Builder validation ──────────────────────────────────

        [Test]
        public void WaitForAll_EmptyConditions_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                new MachineBuilder("Empty")
                    .State("Wait")
                        .WaitForAll(w => { }, "EmptyAll")
                    .Build();
            });
        }

        [Test]
        public void WaitForAny_EmptyConditions_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                new MachineBuilder("Empty")
                    .State("Wait")
                        .WaitForAny(w => { }, "EmptyAny")
                    .Build();
            });
        }

        [Test]
        public void WaitForAll_NullConfigure_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                new MachineBuilder("Null")
                    .State("Wait")
                        .WaitForAll(null, "NullAll")
                    .Build();
            });
        }

        [Test]
        public void WaitForAny_NullConfigure_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                new MachineBuilder("Null")
                    .State("Wait")
                        .WaitForAny(null, "NullAny")
                    .Build();
            });
        }

        // ── Scheduler-driven composite waits ────────────────────

        [Test]
        public void WaitForAll_WorksWithSchedulerUpdate()
        {
            bool afterWait = false;
            var definition = new MachineBuilder("AllScheduled")
                .State("Wait")
                    .WaitForAll(w => w.Time(2.0).Event("Done"), "WaitAll")
                    .Then(ctx => afterWait = true, "After")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Scheduler ticks — time elapses but event not delivered
            scheduler.Update(3.0);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Blocked));

            // Deliver event via scheduler queue
            var done = MachineBuilder.EventIdFrom(definition, "Done");
            scheduler.Events.Enqueue(done);
            scheduler.Update(3.0);

            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
            Assert.That(afterWait, Is.True);
        }
    }
}
