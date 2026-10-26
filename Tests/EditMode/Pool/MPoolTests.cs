using System;
using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    internal sealed class MPoolTests
    {
        private static readonly MPoolKey TestKey = new MPoolKey("tests.pool.prefab");

        private GameObject _prefab;
        private GameObject _storageObject;
        private MPool _pool;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Pool Test Prefab");
            _prefab.AddComponent<MPoolProbe>();
            _storageObject = new GameObject("Pool Test Storage");
            _pool = new MPool(_storageObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _pool?.Dispose();

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
            }

            if (_storageObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_storageObject);
            }
        }

        [Test]
        public void PrewarmSpawnAndDespawn_ReusesInstanceAndRunsLifecycleCallbacks()
        {
            _pool.Register(TestKey, _prefab, 2);
            _pool.Prewarm(TestKey, 1);

            GameObject first = _pool.Spawn(TestKey, new Vector3(2f, 3f, 0f), Quaternion.identity);
            MPoolProbe probe = first.GetComponent<MPoolProbe>();

            Assert.That(first.activeSelf, Is.True);
            Assert.That(first.transform.position, Is.EqualTo(new Vector3(2f, 3f, 0f)));
            Assert.That(probe.SpawnCount, Is.EqualTo(1));
            Assert.That(probe.DespawnCount, Is.Zero);

            _pool.Despawn(first);

            Assert.That(first.activeSelf, Is.False);
            Assert.That(probe.DespawnCount, Is.EqualTo(1));
            Assert.That(first.transform.parent, Is.SameAs(_storageObject.transform));

            GameObject second = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);

            Assert.That(second, Is.SameAs(first));
            Assert.That(probe.SpawnCount, Is.EqualTo(2));
        }

        [Test]
        public void Prewarm_DoesNotInvokeGameplayCallbacksAndCreatesTargetInactiveCount()
        {
            _pool.Register(TestKey, _prefab, 3);
            _pool.Prewarm(TestKey, 2);

            GameObject first = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);
            GameObject second = _pool.Spawn(TestKey, Vector3.one, Quaternion.identity);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(first.GetComponent<MPoolProbe>().SpawnCount, Is.EqualTo(1));
            Assert.That(second.GetComponent<MPoolProbe>().SpawnCount, Is.EqualTo(1));
        }

        [Test]
        public void Despawn_WhenInstanceIsNotOwned_ThrowsClearError()
        {
            var outsider = new GameObject("Outsider");

            try
            {
                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(() => _pool.Despawn(outsider));
                StringAssert.Contains("不属于", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(outsider);
            }
        }

        [Test]
        public void Despawn_WhenUnityPoolMaxSizeIsFull_DestroysOverflowInstance()
        {
            _pool.Register(TestKey, _prefab, 1);
            GameObject first = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);
            GameObject second = _pool.Spawn(TestKey, Vector3.one, Quaternion.identity);

            _pool.Despawn(first);
            _pool.Despawn(second);

            Assert.That(first == null, Is.False);
            Assert.That(second == null, Is.True);
            Assert.That(_pool.Spawn(TestKey, Vector3.zero, Quaternion.identity), Is.SameAs(first));
        }

        [Test]
        public void Clear_DestroysTrackedInstancesAndRemovesRegistration()
        {
            _pool.Register(TestKey, _prefab);
            GameObject active = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);
            GameObject inactive = _pool.Spawn(TestKey, Vector3.one, Quaternion.identity);
            _pool.Despawn(inactive);

            _pool.Clear(TestKey);

            Assert.That(active == null, Is.True);
            Assert.That(inactive == null, Is.True);
            Assert.That(_pool.IsRegistered(TestKey), Is.False);
            Assert.That(_pool.RegisteredCount, Is.Zero);
        }

        [Test]
        public void Spawn_WhenInactiveInstanceWasDestroyedExternally_RebuildsInactivePool()
        {
            _pool.Register(TestKey, _prefab);
            GameObject destroyed = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);
            _pool.Despawn(destroyed);
            UnityEngine.Object.DestroyImmediate(destroyed);

            GameObject replacement = _pool.Spawn(TestKey, Vector3.one, Quaternion.identity);

            Assert.That(replacement, Is.Not.Null);
            Assert.That(ReferenceEquals(replacement, destroyed), Is.False);
        }

        [Test]
        public void Despawn_WhenInstanceWasAlreadyReleased_ThrowsBeforeUnityPoolRelease()
        {
            _pool.Register(TestKey, _prefab);
            GameObject instance = _pool.Spawn(TestKey, Vector3.zero, Quaternion.identity);
            _pool.Despawn(instance);

            Assert.Throws<InvalidOperationException>(() => _pool.Despawn(instance));
        }

        [Test]
        public void Prewarm_WhenTargetExceedsInactiveLimit_Throws()
        {
            _pool.Register(TestKey, _prefab, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => _pool.Prewarm(TestKey, 3));
        }
    }

    internal sealed class MPoolProbe : MonoBehaviour, IMPooledGameObject
    {
        public int SpawnCount { get; private set; }

        public int DespawnCount { get; private set; }

        public void OnSpawned()
        {
            SpawnCount++;
        }

        public void OnDespawned()
        {
            DespawnCount++;
        }
    }
}
