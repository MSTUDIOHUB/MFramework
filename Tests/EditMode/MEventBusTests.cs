using System;
using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    public sealed class MEventBusTests
    {
        [Test]
        public void DisposeStopsFutureDelivery()
        {
            MEventBus events = new MEventBus();
            int total = 0;
            IDisposable subscription = events.Subscribe<int>(value => total += value);

            events.Publish(2);
            subscription.Dispose();
            events.Publish(3);

            Assert.AreEqual(2, total);
        }

        [Test]
        public void SubscriptionChangesApplyOnNextPublication()
        {
            MEventBus events = new MEventBus();
            int first = 0;
            int second = 0;
            IDisposable firstSubscription = null;
            firstSubscription = events.Subscribe<int>(_ =>
            {
                first++;
                firstSubscription.Dispose();
                events.Subscribe<int>(__ => second++);
            });

            events.Publish(1);
            events.Publish(1);

            Assert.AreEqual(1, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void DestroyedUnityOwnerExpiresLazily()
        {
            MEventBus events = new MEventBus();
            ScriptableObject owner = ScriptableObject.CreateInstance<TestOwner>();
            int count = 0;
            events.Subscribe<int>(_ => count++, owner);
            UnityEngine.Object.DestroyImmediate(owner);

            events.Publish(1);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void OwnerAlreadyDestroyedWhenSubscribing_DoesNotCreatePermanentSubscription()
        {
            MEventBus events = new MEventBus();
            ScriptableObject owner = ScriptableObject.CreateInstance<TestOwner>();
            UnityEngine.Object.DestroyImmediate(owner);
            int count = 0;

            events.Subscribe<int>(_ => count++, owner);
            events.Publish(1);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void SubscriberFailuresAreAggregatedAfterOtherHandlersRun()
        {
            MEventBus events = new MEventBus();
            bool laterHandlerRan = false;
            events.Subscribe<int>(_ => throw new InvalidOperationException("expected"));
            events.Subscribe<int>(_ => laterHandlerRan = true);

            MEventDispatchException exception = Assert.Throws<MEventDispatchException>(() => events.Publish(1));

            Assert.IsTrue(laterHandlerRan);
            Assert.AreEqual(typeof(int), exception.EventType);
            Assert.AreEqual(1, exception.InnerExceptions.Count);
        }

        private sealed class TestOwner : ScriptableObject
        {
        }
    }
}
