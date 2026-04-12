// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    /// <summary>
    /// Performance and allocation profiling tests.
    ///
    /// These tests measure GC allocations, boxing overhead, and memory behavior
    /// during runtime execution. They are not micro-benchmarks — they verify
    /// that the runtime doesn't allocate where it shouldn't.
    ///
    /// Key concerns:
    ///   - Runtime ticks (Update) should not allocate when idle
    ///   - Event delivery should not allocate beyond the event itself
    ///   - MachineContext boxes value types (known, measured here)
    ///   - TraceBuffer should not allocate during Record (ring buffer)
    ///   - Machine creation/removal should not leak
    /// </summary>
    [TestFixture]
    public class PerformanceTests
    {
        // ══════════════════════════════════════════════════════
        // GC Allocation Tests
        // ══════════════════════════════════════════════════════

        [Test]
        public void SchedulerUpdate_WhenNoMachinesAreRunnable_DoesNotAllocate()
        {
            // A scheduler with idle/blocked machines should cost nothing per tick.
            var definition = new MachineBuilder("IdleTest")
                .State("Wait")
                    .WaitForEvent("Go", "WaitForGo")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Warm up — first few ticks may allocate due to JIT
            for (int i = 0; i < 100; i++)
                scheduler.Update(i * 0.016);

            // Measure
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                scheduler.Update(100.0 + i * 0.016);
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"Scheduler.Update (1000 idle ticks): {allocated} bytes allocated");

            // EventQueue.FlushAndSwap creates no new objects (double-buffered),
            // and blocked-on-event machines early-return in Update.
            Assert.That(allocated, Is.LessThanOrEqualTo(1024),
                $"Scheduler.Update should not allocate significantly when idle. Got {allocated} bytes.");
        }

        [Test]
        public void SchedulerUpdate_WhenTimeBlocked_DoesNotAllocateUntilResume()
        {
            // A machine blocked on WaitForTime should cost nothing until time elapses.
            var definition = new MachineBuilder("TimeTest")
                .State("Wait")
                    .WaitForTime(100.0, "LongWait") // Very long wait
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            // Warm up
            for (int i = 0; i < 100; i++)
                scheduler.Update(i * 0.016);

            // Measure — machine is blocked, time hasn't elapsed
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                scheduler.Update(1.0 + i * 0.016);
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"Scheduler.Update (1000 time-blocked ticks): {allocated} bytes allocated");

            Assert.That(allocated, Is.LessThanOrEqualTo(1024),
                $"Ticking a time-blocked machine should not allocate. Got {allocated} bytes.");
        }

        [Test]
        public void TraceBuffer_Record_DoesNotAllocate()
        {
            // The ring buffer should reuse its internal array.
            var buffer = new TraceBuffer(128);
            var trace = new TransitionTrace(
                new StateId(0), new StateId(1), 0,
                TransitionReasonKind.Direct, "test", 0.0);

            // Warm up
            for (int i = 0; i < 200; i++)
                buffer.Record(trace);

            buffer.Clear();

            // Measure — recording into a pre-allocated ring buffer
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                buffer.Record(trace);
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"TraceBuffer.Record (1000 records): {allocated} bytes allocated");

            Assert.That(allocated, Is.LessThanOrEqualTo(256),
                $"TraceBuffer.Record should not allocate (ring buffer). Got {allocated} bytes.");
        }

        [Test]
        public void TraceBuffer_GetTraces_AllocatesNewArray()
        {
            // GetTraces returns a copy — this allocation is intentional (snapshot semantics).
            var buffer = new TraceBuffer(16);
            var trace = new TransitionTrace(
                new StateId(0), new StateId(1), 0,
                TransitionReasonKind.Direct, "test", 0.0);

            for (int i = 0; i < 10; i++)
                buffer.Record(trace);

            // Each call should return a new array (detached copy)
            var first = buffer.GetTraces();
            var second = buffer.GetTraces();

            Assert.That(first, Is.Not.SameAs(second),
                "GetTraces should return a new array each call (copy-on-read semantics).");
            Assert.That(first.Length, Is.EqualTo(second.Length));
        }

        // ══════════════════════════════════════════════════════
        // Boxing Tests
        // ══════════════════════════════════════════════════════

        [Test]
        public void MachineContext_SetInt_BoxesValueType()
        {
            // MachineContext uses Dictionary<string, object> which boxes value types.
            // This test documents the cost — each Set<int> boxes.
            var ctx = new MachineContext();

            // Warm up
            ctx.Set("warmup", 0);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
                ctx.Set("counter", i);
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            long perSet = allocated / 100;
            TestContext.WriteLine($"MachineContext.Set<int> (100 calls): {allocated} bytes total, ~{perSet} bytes/call");

            // Each int box is ~24 bytes on 64-bit. This is a known cost.
            Assert.That(perSet, Is.GreaterThan(0),
                "Set<int> should box (Dictionary<string, object>). If this is zero, something changed.");
            TestContext.WriteLine("NOTE: Boxing is a known cost of the string-keyed blackboard design.");
            TestContext.WriteLine("Future optimization: typed slots or struct-based context.");
        }

        [Test]
        public void MachineContext_SetString_DoesNotBox()
        {
            // Strings are reference types — no boxing expected.
            var ctx = new MachineContext();
            ctx.Set("warmup", "x");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
                ctx.Set("name", "same_string"); // Same string instance, no new allocation
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"MachineContext.Set<string> (100 calls, same ref): {allocated} bytes");

            // Should be minimal — no boxing, no new string allocation
            Assert.That(allocated, Is.LessThanOrEqualTo(512),
                "Set<string> with same reference should not allocate significantly.");
        }

        [Test]
        public void MachineContext_GetInt_CastDoesNotAllocate()
        {
            // Unboxing an int doesn't allocate — the box already exists.
            var ctx = new MachineContext();
            ctx.Set("value", 42);

            // Warm up
            for (int i = 0; i < 100; i++)
                ctx.Get<int>("value");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                ctx.Get<int>("value");
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"MachineContext.Get<int> (1000 calls): {allocated} bytes");

            Assert.That(allocated, Is.LessThanOrEqualTo(256),
                "Get<int> (unboxing) should not allocate.");
        }

        // ══════════════════════════════════════════════════════
        // Execution Allocation Tests
        // ══════════════════════════════════════════════════════

        [Test]
        public void ActionStep_Execution_MeasureAllocations()
        {
            // Measure allocation cost of executing action steps.
            int counter = 0;
            var definition = new MachineBuilder("ActionTest")
                .State("Run")
                    .Then(ctx => counter++, "Increment")
                    .GoTo("Run", "Loop")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);

            // Machine will loop Run→Run via GoTo until safety limit.
            // We catch the exception and measure allocations during execution.
            long before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                machine.Start(0.0);
            }
            catch (InvalidOperationException)
            {
                // Expected: "exceeded step execution limit"
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            long perStep = counter > 0 ? allocated / counter : 0;
            TestContext.WriteLine($"Action step execution ({counter} steps): {allocated} bytes total, ~{perStep} bytes/step");
            TestContext.WriteLine("NOTE: Transition recording (TransitionTrace) allocates per transition.");

            // Each transition creates a TransitionTrace (class, ~allocates).
            // Pure action steps (Continue) should not allocate.
            // This test documents the current cost.
        }

        [Test]
        public void EventDelivery_MeasureAllocations()
        {
            // Measure allocation cost of sending events.
            var definition = new MachineBuilder("EventTest")
                .State("Wait")
                    .WaitForEvent("Tick", "WaitForTick")
                    .GoTo("Wait", "Loop")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0.0);

            var tickEvent = MachineBuilder.EventIdFrom(definition, "Tick");

            // Warm up
            for (int i = 0; i < 50; i++)
            {
                machine.SendEvent(tickEvent, i * 0.1);
                scheduler.Update(i * 0.1);
            }

            // Measure
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                machine.SendEvent(tickEvent, 100.0 + i * 0.1);
                scheduler.Update(100.0 + i * 0.1);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            long perCycle = allocated / 500;
            TestContext.WriteLine($"Event delivery (500 send+update cycles): {allocated} bytes total, ~{perCycle} bytes/cycle");
            TestContext.WriteLine("NOTE: Each cycle triggers a transition (Wait→Wait) which allocates a TransitionTrace.");
        }

        // ══════════════════════════════════════════════════════
        // Memory Leak Tests
        // ══════════════════════════════════════════════════════

        [Test]
        public void Scheduler_CreateAndRemove_DoesNotLeak()
        {
            // Create and remove many machines — machine count should stay at zero.
            var definition = new MachineBuilder("LeakTest")
                .State("Idle")
                    .WaitForEvent("Go", "Wait")
                .Build();

            var scheduler = new Scheduler();

            for (int i = 0; i < 1000; i++)
            {
                var machine = scheduler.CreateMachine(definition);
                machine.Start(0.0);
                scheduler.RemoveMachine(machine.Id);
            }

            Assert.That(scheduler.MachineCount, Is.EqualTo(0),
                "All machines should be removed. Machine count should be zero.");
        }

        [Test]
        public void Scheduler_ManyMachines_MemoryScalesLinearly()
        {
            // Verify memory usage scales linearly with machine count, not exponentially.
            var definition = new MachineBuilder("ScaleTest")
                .State("Idle")
                    .WaitForEvent("Go", "Wait")
                .Build();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long baseline = GC.GetTotalMemory(true);

            var scheduler = new Scheduler();
            var machines = new List<Machine>();

            // Create 100 machines
            for (int i = 0; i < 100; i++)
            {
                var m = scheduler.CreateMachine(definition);
                m.Start(0.0);
                machines.Add(m);
            }

            long after100 = GC.GetTotalMemory(true);
            long per100 = after100 - baseline;

            // Create 100 more (200 total)
            for (int i = 0; i < 100; i++)
            {
                var m = scheduler.CreateMachine(definition);
                m.Start(0.0);
                machines.Add(m);
            }

            long after200 = GC.GetTotalMemory(true);
            long second100 = after200 - after100;

            TestContext.WriteLine($"First 100 machines: {per100} bytes ({per100 / 100} bytes/machine)");
            TestContext.WriteLine($"Second 100 machines: {second100} bytes ({second100 / 100} bytes/machine)");

            // Second batch should be roughly similar to first (linear scaling).
            // Allow 3x tolerance for GC/runtime overhead variance.
            Assert.That(second100, Is.LessThan(per100 * 3),
                "Memory should scale roughly linearly. Second batch should not be dramatically larger.");
        }

        // ══════════════════════════════════════════════════════
        // StepResult Struct Allocation Test
        // ══════════════════════════════════════════════════════

        [Test]
        public void StepResult_FactoryMethods_DoNotAllocate()
        {
            // StepResult is a struct — factory methods should not heap-allocate.

            // Warm up
            for (int i = 0; i < 100; i++)
            {
                var _ = StepResult.Continue();
                var __ = StepResult.Transition(new StateId(0));
                var ___ = StepResult.WaitForEvent(new EventId(0));
                var ____ = StepResult.WaitForTime(1.0);
                var _____ = StepResult.WaitForPredicate();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                var a = StepResult.Continue();
                var b = StepResult.Transition(new StateId(0));
                var c = StepResult.WaitForEvent(new EventId(0));
                var d = StepResult.WaitForTime(1.0);
                var e = StepResult.WaitForPredicate();
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"StepResult factory methods (5000 calls): {allocated} bytes");

            Assert.That(allocated, Is.LessThanOrEqualTo(256),
                "StepResult is a struct — factory methods should not heap-allocate.");
        }

        // ══════════════════════════════════════════════════════
        // Identity Struct Tests
        // ══════════════════════════════════════════════════════

        [Test]
        public void TypedIds_EqualityComparison_DoesNotBox()
        {
            // StateId, EventId, MachineId implement IEquatable<T> to avoid boxing.
            var a = new StateId(1);
            var b = new StateId(2);

            // Warm up
            for (int i = 0; i < 100; i++)
                _ = a.Equals(b);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = a.Equals(b);
                _ = a == b;
                _ = a != b;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            TestContext.WriteLine($"StateId equality (3000 comparisons): {allocated} bytes");

            Assert.That(allocated, Is.LessThanOrEqualTo(256),
                "Typed ID equality should not box (IEquatable<T>).");
        }

        // ══════════════════════════════════════════════════════
        // Throughput Baseline
        // ══════════════════════════════════════════════════════

        [Test]
        public void Throughput_ManyMachines_MeasureUpdateCost()
        {
            // Measure scheduler throughput with many blocked machines.
            var definition = new MachineBuilder("ThroughputTest")
                .State("Idle")
                    .WaitForEvent("Go", "Wait")
                .Build();

            var scheduler = new Scheduler();
            for (int i = 0; i < 500; i++)
            {
                var m = scheduler.CreateMachine(definition);
                m.Start(0.0);
            }

            // Warm up
            for (int i = 0; i < 100; i++)
                scheduler.Update(i * 0.016);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
                scheduler.Update(100.0 + i * 0.016);
            sw.Stop();

            double ticksPerUpdate = (double)sw.ElapsedTicks / 10000;
            double usPerUpdate = (double)sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency * 1_000_000 / 10000;

            TestContext.WriteLine($"Scheduler.Update with 500 blocked machines:");
            TestContext.WriteLine($"  10000 updates in {sw.ElapsedMilliseconds}ms");
            TestContext.WriteLine($"  ~{usPerUpdate:F2} us/update");
            TestContext.WriteLine($"  ~{1_000_000 / usPerUpdate:F0} updates/second");

            // Sanity: should complete in reasonable time (< 1 second for 10k updates)
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000),
                "10000 scheduler updates with 500 machines should complete quickly.");
        }
    }
}
