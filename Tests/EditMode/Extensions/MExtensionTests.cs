using System;
using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    internal sealed class MExtensionTests
    {
        [Test]
        public void GetOrAddComponent_ReturnsExistingComponentAfterFirstCall()
        {
            var gameObject = new GameObject("Extension Test");

            try
            {
                BoxCollider2D first = gameObject.GetOrAddComponent<BoxCollider2D>();
                BoxCollider2D second = gameObject.GetOrAddComponent<BoxCollider2D>();

                Assert.That(first, Is.SameAs(second));
                Assert.That(gameObject.GetComponents<BoxCollider2D>(), Has.Length.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void VectorExtensions_ReplaceOnlyRequestedAxis()
        {
            var source = new Vector3(1f, 2f, 3f);

            Assert.That(source.WithX(9f), Is.EqualTo(new Vector3(9f, 2f, 3f)));
            Assert.That(source.WithY(9f), Is.EqualTo(new Vector3(1f, 9f, 3f)));
            Assert.That(source.WithZ(9f), Is.EqualTo(new Vector3(1f, 2f, 9f)));
            Assert.That(source, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void RectTransformExtensions_PreserveUnchangedAxis()
        {
            var gameObject = new GameObject("RectTransform Extension Test", typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();

            try
            {
                rectTransform.anchoredPosition = new Vector2(2f, 3f);
                rectTransform.SetAnchoredPositionX(8f);
                Assert.That(rectTransform.anchoredPosition, Is.EqualTo(new Vector2(8f, 3f)));

                rectTransform.SetAnchoredPositionY(7f);
                Assert.That(rectTransform.anchoredPosition, Is.EqualTo(new Vector2(8f, 7f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AddTo_ReturnsValueAndBagOwnsItsLifetime()
        {
            var bag = new MDisposableBag();
            var disposable = new MCountingDisposable();

            MCountingDisposable returned = disposable.AddTo(bag);
            bag.Dispose();

            Assert.That(returned, Is.SameAs(disposable));
            Assert.That(disposable.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void AddTo_ReturnsValueAndAssetScopeOwnsItsLifetime()
        {
            var scope = new MAssetScope();
            var disposable = new MCountingDisposable();

            MCountingDisposable returned = disposable.AddTo(scope);
            scope.Dispose();

            Assert.That(returned, Is.SameAs(disposable));
            Assert.That(disposable.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void AddTo_WhenBagIsNull_Throws()
        {
            var disposable = new MCountingDisposable();

            Assert.Throws<ArgumentNullException>(() => disposable.AddTo((MDisposableBag)null));
            Assert.Throws<ArgumentNullException>(() => disposable.AddTo((MAssetScope)null));
        }
    }

    internal sealed class MCountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
