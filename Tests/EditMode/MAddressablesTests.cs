using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MFramework.Tests.EditMode
{
    public sealed class MAddressablesTests
    {
        [Test]
        public void AssetScopeDisposesInReverseOrderAndOnlyOnce()
        {
            List<int> order = new List<int>();
            MAssetScope scope = new MAssetScope();
            scope.Own(new TestDisposable(1, order));
            scope.Own(new TestDisposable(2, order));

            scope.Dispose();
            scope.Dispose();

            CollectionAssert.AreEqual(new[] { 2, 1 }, order);
        }

        [Test]
        public void AssetScopeContinuesDisposingAfterOneResourceThrows()
        {
            List<int> order = new List<int>();
            MAssetScope scope = new MAssetScope();
            scope.Own(new TestDisposable(1, order));
            scope.Own(new TestDisposable(2, order, true));
            scope.Own(new TestDisposable(3, order));

            AggregateException exception = Assert.Throws<AggregateException>(() => scope.Dispose());

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, order);
            Assert.AreEqual(1, exception.InnerExceptions.Count);
        }

        [Test]
        public void EmptyStringKeyFailsBeforeStartingAddressables()
        {
            Assert.Throws<ArgumentException>(
                () => MAddressables.LoadAssetAsync<TextAsset>("   "));
        }

        [Test]
        public void NullKeyFailsBeforeStartingAddressables()
        {
            Assert.Throws<ArgumentNullException>(
                () => MAddressables.LoadAssetAsync<TextAsset>((object)null));
        }

        [Test]
        public void DelayedActivationSingleSceneFailsBeforeStartingAddressables()
        {
            Assert.Throws<ArgumentException>(() => MAddressables.LoadSceneAsync(
                "test.scene",
                LoadSceneMode.Single,
                false));
        }

        private sealed class TestDisposable : IDisposable
        {
            private readonly int _id;
            private readonly List<int> _order;
            private readonly bool _throws;

            public TestDisposable(int id, List<int> order, bool throws = false)
            {
                _id = id;
                _order = order;
                _throws = throws;
            }

            public void Dispose()
            {
                _order.Add(_id);
                if (_throws)
                {
                    throw new InvalidOperationException("expected");
                }
            }
        }
    }
}
