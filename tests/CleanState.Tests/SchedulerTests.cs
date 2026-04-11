// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Runtime;

namespace CleanState.Tests
{
    [TestFixture]
    public class SchedulerTests
    {
        [Test]
        public void Scheduler_DeliversBroadcastEvent_ToAllMachines()
        {
            int machineADone = 0;
            int machineBDone = 0;

            var defA = new MachineBuilder("A")
                .State("Wait")
                    .WaitForEvent("Go", "WaitGo")
                    .Then(ctx => machineADone++, "Done")
                .Build();

            var defB = new MachineBuilder("B")
                .State("Wait")
                    .WaitForEvent("Go", "WaitGo")
                    .Then(ctx => machineBDone++, "Done")
                .Build();

            var scheduler = new Scheduler();
            var a = scheduler.CreateMachine(defA);
            var b = scheduler.CreateMachine(defB);
            a.Start(0f);
            b.Start(0f);

            Assert.That(a.Status, Is.EqualTo(MachineStatus.Blocked));
            Assert.That(b.Status, Is.EqualTo(MachineStatus.Blocked));

            // Enqueue broadcast event (need the event IDs)
            var goA = MachineBuilder.EventIdFrom(defA, "Go");
            var goB = MachineBuilder.EventIdFrom(defB, "Go");

            // Send directly since event IDs differ per definition
            a.SendEvent(goA, 1f);
            b.SendEvent(goB, 1f);

            Assert.That(machineADone, Is.EqualTo(1));
            Assert.That(machineBDone, Is.EqualTo(1));
        }

        [Test]
        public void Scheduler_ManagesMachineLifecycle()
        {
            var definition = new MachineBuilder("Simple")
                .State("Only")
                    .Then(ctx => { }, "Noop")
                .Build();

            var scheduler = new Scheduler();
            Assert.That(scheduler.MachineCount, Is.EqualTo(0));

            var machine = scheduler.CreateMachine(definition);
            Assert.That(scheduler.MachineCount, Is.EqualTo(1));
            Assert.That(scheduler.GetMachine(machine.Id), Is.SameAs(machine));

            scheduler.RemoveMachine(machine.Id);
            Assert.That(scheduler.MachineCount, Is.EqualTo(0));
        }

        [Test]
        public void Scheduler_EventQueue_DeliversTargetedEvent()
        {
            bool done = false;
            var definition = new MachineBuilder("Targeted")
                .State("Wait")
                    .WaitForEvent("Ping", "WaitPing")
                    .Then(ctx => done = true, "Done")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var pingId = MachineBuilder.EventIdFrom(definition, "Ping");
            scheduler.Events.Enqueue(pingId, machine.Id);
            scheduler.Update(1f);

            Assert.That(done, Is.True);
            Assert.That(machine.Status, Is.EqualTo(MachineStatus.Completed));
        }
    }
}