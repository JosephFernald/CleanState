// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Runtime;
using CleanState.Recovery;

namespace CleanState.Tests
{
    [TestFixture]
    public class RecoveryTests
    {
        [Test]
        public void Snapshot_CapturesAndRestores_DomainData()
        {
            string result = null;
            var definition = new MachineBuilder("Recoverable")
                .State("Setup")
                    .Checkpoint()
                    .Then(ctx => ctx.Set("score", 42), "SetScore")
                    .GoTo("Process")
                .State("Process")
                    .Checkpoint()
                    .Then(ctx => result = $"Score={ctx.Get<int>("score")}", "ReadScore")
                .Build();

            // Run to completion to set up context
            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);
            Assert.That(result, Is.EqualTo("Score=42"));

            // Now simulate recovery: create fresh machine, restore from snapshot
            var snapshot = new MachineSnapshot
            {
                MachineName = "Recoverable",
                StateName = "Process",
                StepIndex = 0,
                DomainData = { { "score", 42 } }
            };

            result = null;
            var machine2 = scheduler.CreateMachine(definition);
            MachineRecovery.RestoreFromSnapshot(machine2, snapshot, 10f);

            Assert.That(result, Is.EqualTo("Score=42"));
            Assert.That(machine2.Status, Is.EqualTo(MachineStatus.Completed));
        }

        [Test]
        public void CaptureSnapshot_GrabsSpecifiedKeys()
        {
            var definition = new MachineBuilder("Snap")
                .State("Wait")
                    .Checkpoint()
                    .Then(ctx =>
                    {
                        ctx.Set("a", 1);
                        ctx.Set("b", "hello");
                        ctx.Set("c", 99);
                    }, "SetData")
                    .WaitForEvent("Done", "WaitDone")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            var snapshot = MachineRecovery.CaptureSnapshot(machine, "a", "b");
            Assert.That(snapshot.MachineName, Is.EqualTo("Snap"));
            Assert.That(snapshot.StateName, Is.EqualTo("Wait"));
            Assert.That(snapshot.DomainData.ContainsKey("a"), Is.True);
            Assert.That(snapshot.DomainData.ContainsKey("b"), Is.True);
            Assert.That(snapshot.DomainData.ContainsKey("c"), Is.False);
        }

        [Test]
        public void RestoreSnapshot_UnknownState_Throws()
        {
            var definition = new MachineBuilder("Bad")
                .State("Only")
                    .Then(ctx => { }, "Noop")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);

            var snapshot = new MachineSnapshot
            {
                MachineName = "Bad",
                StateName = "NonExistent",
                StepIndex = 0
            };

            Assert.Throws<System.ArgumentException>(() =>
                MachineRecovery.RestoreFromSnapshot(machine, snapshot, 0f));
        }
    }
}