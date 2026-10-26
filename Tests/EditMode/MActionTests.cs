using System;
using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    public sealed class MActionTests
    {
        [Test]
        public void Callback_CompletesWhenRun()
        {
            int callCount = 0;
            MActionRunner runner = new MActionRunner();

            MActionHandle handle = runner.Run(MAction.Callback(() => callCount++));

            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Completed));
            Assert.That(runner.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Sequence_RunsChildrenInOrder()
        {
            string trace = string.Empty;
            MActionRunner runner = new MActionRunner();
            MActionHandle handle = runner.Run(MAction.Sequence(
                MAction.Callback(() => trace += "A"),
                MAction.Delay(1f),
                MAction.Callback(() => trace += "B")));

            Assert.That(trace, Is.EqualTo("A"));
            runner.Tick(0.4f, 0.4f);
            Assert.That(trace, Is.EqualTo("A"));
            runner.Tick(0.6f, 0.6f);

            Assert.That(trace, Is.EqualTo("AB"));
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Completed));
        }

        [Test]
        public void Parallel_CompletesAfterLongestChild()
        {
            MActionRunner runner = new MActionRunner();
            MActionHandle handle = runner.Run(MAction.Parallel(
                MAction.Delay(0.25f),
                MAction.Delay(1f)));

            runner.Tick(0.5f, 0.5f);
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Running));

            runner.Tick(0.5f, 0.5f);
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Completed));
        }

        [Test]
        public void Parallel_CancelsOtherChildrenWhenOneFails()
        {
            bool siblingCancelled = false;
            MActionRunner runner = new MActionRunner();
            IMActionNode sibling = MAction.Custom(
                _ => MActionTickResult.Running,
                onCancel: () => siblingCancelled = true);
            IMActionNode failing = MAction.Custom(
                _ => throw new InvalidOperationException("Expected test failure."));
            MActionHandle handle = runner.Run(MAction.Parallel(sibling, failing));

            Assert.Throws<InvalidOperationException>(() => runner.Tick(0.1f, 0.1f));

            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Failed));
            Assert.That(siblingCancelled, Is.True);
            Assert.That(runner.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Repeat_UsesFreshNodeForEveryIteration()
        {
            int factoryCount = 0;
            int callbackCount = 0;
            MActionRunner runner = new MActionRunner();

            MActionHandle handle = runner.Run(MAction.Repeat(3, () =>
            {
                factoryCount++;
                return MAction.Callback(() => callbackCount++);
            }));

            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Completed));
            Assert.That(factoryCount, Is.EqualTo(3));
            Assert.That(callbackCount, Is.EqualTo(3));
        }

        [Test]
        public void WaitUntil_CompletesAfterConditionChanges()
        {
            bool ready = false;
            MActionRunner runner = new MActionRunner();
            MActionHandle handle = runner.Run(MAction.WaitUntil(() => ready));

            runner.Tick(0.1f, 0.1f);
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Running));

            ready = true;
            runner.Tick(0.1f, 0.1f);
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Completed));
        }

        [Test]
        public void Runner_UsesSelectedTimeMode()
        {
            MActionRunner runner = new MActionRunner();
            MActionHandle scaled = runner.Run(MAction.Delay(1f), timeMode: MTimeMode.Scaled);
            MActionHandle unscaled = runner.Run(MAction.Delay(1f), timeMode: MTimeMode.Unscaled);

            runner.Tick(0.25f, 1f);

            Assert.That(scaled.Status, Is.EqualTo(MActionStatus.Running));
            Assert.That(unscaled.Status, Is.EqualTo(MActionStatus.Completed));

            runner.Tick(0.75f, 0f);
            Assert.That(scaled.Status, Is.EqualTo(MActionStatus.Completed));
        }

        [Test]
        public void HandleCancel_PropagatesToActiveChild()
        {
            bool childCancelled = false;
            MActionRunner runner = new MActionRunner();
            IMActionNode child = MAction.Custom(
                _ => MActionTickResult.Running,
                onCancel: () => childCancelled = true);
            MActionHandle handle = runner.Run(MAction.Sequence(MAction.Delay(0f), child));

            handle.Cancel();

            Assert.That(childCancelled, Is.True);
            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Cancelled));
            Assert.That(runner.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void DestroyedOwner_CancelsActionOnNextTick()
        {
            GameObject owner = new GameObject("MAction Owner");
            MActionRunner runner = new MActionRunner();
            MActionHandle handle = runner.Run(MAction.Delay(10f), owner);

            UnityEngine.Object.DestroyImmediate(owner);
            runner.Tick(0.1f, 0.1f);

            Assert.That(handle.Status, Is.EqualTo(MActionStatus.Cancelled));
            Assert.That(runner.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Custom_ReceivesStartTickAndCancelCallbacks()
        {
            int startCount = 0;
            float receivedDelta = 0f;
            int cancelCount = 0;
            MActionRunner runner = new MActionRunner();
            MActionHandle handle = runner.Run(MAction.Custom(
                deltaTime =>
                {
                    receivedDelta = deltaTime;
                    return MActionTickResult.Running;
                },
                () => startCount++,
                () => cancelCount++));

            runner.Tick(0.2f, 0.8f);
            handle.Dispose();

            Assert.That(startCount, Is.EqualTo(1));
            Assert.That(receivedDelta, Is.EqualTo(0.2f));
            Assert.That(cancelCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_CancelsEveryActiveAction()
        {
            MActionRunner runner = new MActionRunner();
            MActionHandle first = runner.Run(MAction.Delay(10f));
            MActionHandle second = runner.Run(MAction.Delay(10f));

            runner.Shutdown();

            Assert.That(first.Status, Is.EqualTo(MActionStatus.Cancelled));
            Assert.That(second.Status, Is.EqualTo(MActionStatus.Cancelled));
            Assert.That(runner.ActiveCount, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => runner.Run(MAction.Delay(1f)));
        }
    }
}
