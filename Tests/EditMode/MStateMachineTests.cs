using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    public sealed class MStateMachineTests
    {
        [Test]
        public void ChangeState_UsesEnterTickExitOrderAndSharedContext()
        {
            TestContext context = new TestContext();
            RecordingState first = new RecordingState("First");
            SecondRecordingState second = new SecondRecordingState();
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(context)
                .Register(first)
                .Register(second);

            Assert.That(machine.ChangeState<RecordingState>(), Is.True);
            machine.Tick(0.25f);
            Assert.That(machine.ChangeState<SecondRecordingState>(), Is.True);

            CollectionAssert.AreEqual(
                new[] { "First.Enter", "First.Tick:0.25", "First.Exit", "Second.Enter" },
                context.Trace);
            Assert.That(machine.Context, Is.SameAs(context));
            Assert.That(machine.CurrentState, Is.SameAs(second));
        }

        [Test]
        public void ChangeState_ToCurrentStateIsIgnored()
        {
            TestContext context = new TestContext();
            RecordingState state = new RecordingState("Only");
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(context)
                .Register(state);

            Assert.That(machine.ChangeState<RecordingState>(), Is.True);
            Assert.That(machine.ChangeState<RecordingState>(), Is.False);

            CollectionAssert.AreEqual(new[] { "Only.Enter" }, context.Trace);
        }

        [Test]
        public void Register_DuplicateStateTypeThrows()
        {
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(new TestContext())
                .Register(new RecordingState("First"));

            Assert.Throws<InvalidOperationException>(
                () => machine.Register(new RecordingState("Second")));
        }

        [Test]
        public void ChangeState_UnregisteredStateThrows()
        {
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(new TestContext());

            Assert.Throws<InvalidOperationException>(() => machine.ChangeState<RecordingState>());
        }

        [Test]
        public void TransitionRequestedInsideEnterIsRejected()
        {
            TestContext context = new TestContext();
            MStateMachine<TestContext> machine = null;
            RecursiveState recursive = new RecursiveState(
                () => machine.ChangeState<SecondRecordingState>());
            machine = new MStateMachine<TestContext>(context)
                .Register(recursive)
                .Register(new SecondRecordingState());

            Assert.Throws<InvalidOperationException>(() => machine.ChangeState<RecursiveState>());
            Assert.That(machine.CurrentState, Is.Null);
        }

        [Test]
        public void Stop_ExitsCurrentStateOnce()
        {
            TestContext context = new TestContext();
            RecordingState state = new RecordingState("Only");
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(context)
                .Register(state);
            machine.ChangeState<RecordingState>();

            machine.Stop();
            machine.Stop();

            CollectionAssert.AreEqual(new[] { "Only.Enter", "Only.Exit" }, context.Trace);
            Assert.That(machine.CurrentState, Is.Null);
        }

        [Test]
        public void ContainsAndIsIn_UseExplicitRegistration()
        {
            MStateMachine<TestContext> machine = new MStateMachine<TestContext>(new TestContext())
                .Register(new RecordingState("Only"));

            Assert.That(machine.Contains<RecordingState>(), Is.True);
            Assert.That(machine.Contains<SecondRecordingState>(), Is.False);
            Assert.That(machine.IsIn<RecordingState>(), Is.False);

            machine.ChangeState<RecordingState>();
            Assert.That(machine.IsIn<RecordingState>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(RecordingState)));
        }

        private sealed class TestContext
        {
            public readonly List<string> Trace = new List<string>();
        }

        private class RecordingState : IMState<TestContext>
        {
            private readonly string _name;

            public RecordingState(string name)
            {
                _name = name;
            }

            public void Enter(TestContext context)
            {
                context.Trace.Add(_name + ".Enter");
            }

            public void Tick(TestContext context, float deltaTime)
            {
                context.Trace.Add(_name + ".Tick:" + deltaTime.ToString("0.00"));
            }

            public void Exit(TestContext context)
            {
                context.Trace.Add(_name + ".Exit");
            }
        }

        private sealed class SecondRecordingState : RecordingState
        {
            public SecondRecordingState()
                : base("Second")
            {
            }
        }

        private sealed class RecursiveState : IMState<TestContext>
        {
            private readonly Action _onEnter;

            public RecursiveState(Action onEnter)
            {
                _onEnter = onEnter;
            }

            public void Enter(TestContext context)
            {
                _onEnter();
            }

            public void Tick(TestContext context, float deltaTime)
            {
            }

            public void Exit(TestContext context)
            {
            }
        }
    }
}
