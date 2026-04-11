// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using CleanState.Builder;
using CleanState.Debug;
using CleanState.Identity;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    /// <summary>
    /// These tests enshrine the source-of-truth architecture:
    ///
    /// The compiled MachineDefinition + runtime Machine state is the ONLY source of truth.
    /// Any visualization (Unity GraphView, etc.) is a disposable projection.
    ///
    /// NEVER:
    ///   - Let visualization state feed back into the FSM
    ///   - Let scene objects or serialized editor data define the graph
    ///   - Let cached graph data outlive the MachineDefinition it came from
    ///
    /// ALWAYS:
    ///   - Derive graph topology from MachineDefinition
    ///   - Derive live state from IFsmObservable / DebugSnapshot
    ///   - Rebuild graph when definition changes
    /// </summary>
    [TestFixture]
    public class SourceOfTruthTests
    {
        // --- MachineDefinition is immutable after build ---

        [Test]
        public void MachineDefinition_IsSealed()
        {
            Assert.That(typeof(MachineDefinition).IsSealed, Is.True,
                "MachineDefinition must be sealed — no subclass should add mutable state");
        }

        [Test]
        public void StateDefinition_IsSealed()
        {
            Assert.That(typeof(StateDefinition).IsSealed, Is.True,
                "StateDefinition must be sealed — no subclass should add mutable state");
        }

        [Test]
        public void MachineDefinition_AllPropertiesAreGetOnly()
        {
            var props = typeof(MachineDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                Assert.That(prop.GetSetMethod(), Is.Null,
                    $"MachineDefinition.{prop.Name} must not have a public setter — definitions are immutable");
            }
        }

        [Test]
        public void StateDefinition_AllPropertiesAreGetOnly()
        {
            var props = typeof(StateDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                Assert.That(prop.GetSetMethod(), Is.Null,
                    $"StateDefinition.{prop.Name} must not have a public setter — definitions are immutable");
            }
        }

        // --- Graph topology is derivable from definition alone ---

        [Test]
        public void GraphTopology_IsDeterministic_FromDefinition()
        {
            var definition = new MachineBuilder("Deterministic")
                .State("A")
                    .Then(ctx => { }, "ActionA")
                    .GoTo("B", "AtoB")
                .State("B")
                    .Then(ctx => ctx.Set("x", 1), "SetX")
                    .Decision("Branch")
                        .When(ctx => ctx.Get<int>("x") > 0, "C", "IsPositive")
                        .Otherwise("D", "IsFallback")
                .State("C")
                    .Then(ctx => { }, "InC")
                .State("D")
                    .Then(ctx => { }, "InD")
                .Build();

            // Extract topology — node count and names come from definition
            Assert.That(definition.StateCount, Is.EqualTo(4));
            Assert.That(definition.GetStateByIndex(0).Name, Is.EqualTo("A"));
            Assert.That(definition.GetStateByIndex(1).Name, Is.EqualTo("B"));
            Assert.That(definition.GetStateByIndex(2).Name, Is.EqualTo("C"));
            Assert.That(definition.GetStateByIndex(3).Name, Is.EqualTo("D"));

            // Edges are derivable by walking steps
            var stateA = definition.GetStateByIndex(0);
            var hasTransition = stateA.Steps.Any(s => s is TransitionStep);
            Assert.That(hasTransition, Is.True,
                "Transition edges must be derivable from StateDefinition.Steps");

            var stateB = definition.GetStateByIndex(1);
            var hasDecision = stateB.Steps.Any(s => s is DecisionStep);
            Assert.That(hasDecision, Is.True,
                "Decision edges must be derivable from StateDefinition.Steps");
        }

        [Test]
        public void GraphTopology_ExtractedTwice_IsIdentical()
        {
            var definition = new MachineBuilder("Stable")
                .State("X")
                    .Then(ctx => { }, "DoX")
                    .GoTo("Y", "XtoY")
                .State("Y")
                    .Then(ctx => { }, "DoY")
                .Build();

            // Two independent extractions must produce identical results
            int nodeCount1 = definition.StateCount;
            int nodeCount2 = definition.StateCount;
            Assert.That(nodeCount1, Is.EqualTo(nodeCount2));

            var state1 = definition.GetStateByIndex(0);
            var state2 = definition.GetStateByIndex(0);
            Assert.That(state1.Name, Is.EqualTo(state2.Name));
            Assert.That(state1.Id, Is.EqualTo(state2.Id));
            Assert.That(state1.Steps.Length, Is.EqualTo(state2.Steps.Length));
        }

        // --- Live state is derivable from IFsmObservable alone ---

        [Test]
        public void LiveState_IsDerivedFromObservable_NotCached()
        {
            var definition = new MachineBuilder("LiveTest")
                .State("A")
                    .WaitForEvent("Go", "WaitGo")
                .State("B")
                    .Then(ctx => { }, "InB")
                .Build();

            var scheduler = new Scheduler();
            var machine = scheduler.CreateMachine(definition);
            machine.Start(0f);

            IFsmObservable observable = machine;

            // Snapshot reflects current state
            var snap1 = observable.GetDebugSnapshot();
            Assert.That(snap1.CurrentStateName, Is.EqualTo("A"));
            Assert.That(snap1.Status, Is.EqualTo(MachineStatus.Blocked));

            // Advance machine
            var goId = MachineBuilder.EventIdFrom(definition, "Go");
            machine.SendEvent(goId, 1f);

            // New snapshot reflects new state — no stale cache
            var snap2 = observable.GetDebugSnapshot();
            Assert.That(snap2.Status, Is.EqualTo(MachineStatus.Completed));
        }

        [Test]
        public void DebugSnapshot_ContainsNoReferenceToMachine()
        {
            var snapshotType = typeof(DebugSnapshot);
            var props = snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                Assert.That(prop.PropertyType, Is.Not.EqualTo(typeof(Machine)),
                    $"DebugSnapshot.{prop.Name} must not reference Machine — snapshots are detached projections");
                Assert.That(prop.PropertyType, Is.Not.EqualTo(typeof(MachineContext)),
                    $"DebugSnapshot.{prop.Name} must not reference MachineContext");
            }
        }

        // --- Step debug info is embedded at build time, not queried at view time ---

        [Test]
        public void StepDebugInfo_IsImmutableAfterConstruction()
        {
            var props = typeof(StepDebugInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                Assert.That(prop.GetSetMethod(), Is.Null,
                    $"StepDebugInfo.{prop.Name} must not have a setter — debug info is set at build time");
            }
        }

        // --- NameLookup is set at build time ---

        [Test]
        public void NameLookup_ProducesConsistentNames()
        {
            var definition = new MachineBuilder("Lookup")
                .State("Alpha")
                    .WaitForEvent("Trigger", "WaitTrigger")
                .Build();

            var lookup = definition.NameLookup;
            var stateId = definition.InitialState;
            var name1 = lookup.GetStateName(stateId);
            var name2 = lookup.GetStateName(stateId);
            Assert.That(name1, Is.EqualTo(name2));
            Assert.That(name1, Is.EqualTo("Alpha"),
                "NameLookup must return the authored name, not a generated one");
        }

        // --- TransitionTrace is immutable once recorded ---

        [Test]
        public void TransitionTrace_AllPropertiesAreGetOnly()
        {
            var props = typeof(TransitionTrace).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                Assert.That(prop.GetSetMethod(), Is.Null,
                    $"TransitionTrace.{prop.Name} must not have a setter — traces are historical records");
            }
        }

        // --- No scene-object or serialization dependency in core ---

        [Test]
        public void CoreTypes_HaveNoSerializationAttributes()
        {
            var coreTypes = typeof(Machine).Assembly.GetTypes();
            var unitySerializeAttrs = new[] { "SerializeField", "SerializeReference" };

            foreach (var type in coreTypes)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static);
                foreach (var field in fields)
                {
                    var attrs = field.GetCustomAttributes(false);
                    foreach (var attr in attrs)
                    {
                        var attrName = attr.GetType().Name;
                        Assert.That(unitySerializeAttrs.Contains(attrName), Is.False,
                            $"Core type {type.Name}.{field.Name} has [{attrName}] — " +
                            "core must not depend on Unity serialization");
                    }
                }
            }
        }
    }
}