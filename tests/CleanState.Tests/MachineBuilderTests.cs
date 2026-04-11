// Copyright (c) 2025 Sin City Materialization LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using CleanState.Builder;
using CleanState.Runtime;
using CleanState.Steps;

namespace CleanState.Tests
{
    [TestFixture]
    public class MachineBuilderTests
    {
        [Test]
        public void Build_SingleState_WithAction_Compiles()
        {
            var definition = new MachineBuilder("TestMachine")
                .State("Start")
                    .Then(ctx => { }, "DoSomething")
                .Build();

            Assert.That(definition.Name, Is.EqualTo("TestMachine"));
            Assert.That(definition.StateCount, Is.EqualTo(1));
        }

        [Test]
        public void Build_MultipleStates_WithTransition_Compiles()
        {
            var definition = new MachineBuilder("TestMachine")
                .State("A")
                    .Then(ctx => { }, "ActionA")
                    .GoTo("B", "GoToB")
                .State("B")
                    .Then(ctx => { }, "ActionB")
                .Build();

            Assert.That(definition.StateCount, Is.EqualTo(2));
        }

        [Test]
        public void Build_WithDecision_Compiles()
        {
            var definition = new MachineBuilder("TestMachine")
                .State("Start")
                    .Then(ctx => ctx.Set("x", 1), "SetX")
                    .Decision("Check")
                        .When(ctx => ctx.Get<int>("x") > 0, "Positive", "IsPositive")
                        .Otherwise("Negative", "IsNegative")
                .State("Positive")
                    .Then(ctx => { }, "InPositive")
                .State("Negative")
                    .Then(ctx => { }, "InNegative")
                .Build();

            Assert.That(definition.StateCount, Is.EqualTo(3));
        }

        [Test]
        public void Build_WithWaitForEvent_Compiles()
        {
            var definition = new MachineBuilder("TestMachine")
                .State("Start")
                    .WaitForEvent("PlayerReady", "WaitForPlayer")
                    .Then(ctx => { }, "AfterWait")
                .Build();

            Assert.That(definition.StateCount, Is.EqualTo(1));
        }

        [Test]
        public void Build_NoStates_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                new MachineBuilder("Empty").Build();
            });
        }

        [Test]
        public void Build_GoToUnknownState_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                new MachineBuilder("Bad")
                    .State("Start")
                        .GoTo("NonExistent")
                    .Build();
            });
        }
    }
}