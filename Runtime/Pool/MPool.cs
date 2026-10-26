using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
#if UNITY_6000_4_OR_NEWER
using PoolInstanceId = UnityEngine.EntityId;
#else
using PoolInstanceId = System.Int32;
#endif

namespace MFramework
{
    /// <summary>
    /// 复用显式注册的 GameObject 预制体，减少重复 Instantiate 与 Destroy 的开销。
    /// </summary>
    /// <remarks>
    /// 每项注册的闲置实例由 Unity 官方 <see cref="ObjectPool{T}"/> 保存；MFramework 只补充预制体注册、
    /// 实例归属校验和缓存的 <see cref="IMPooledGameObject"/> 生命周期回调。此类不池化任意 C# 对象。
    /// 释放持有已注册预制体的 Addressables Lease 前，必须先调用 <see cref="Clear(MPoolKey)"/>。
    /// </remarks>
    public sealed class MPool : IMPool, IMFrameworkModule, IDisposable
    {
        private readonly Dictionary<MPoolKey, PoolEntry> _entries = new Dictionary<MPoolKey, PoolEntry>();
        private readonly Dictionary<PoolInstanceId, InstanceRecord> _instances = new Dictionary<PoolInstanceId, InstanceRecord>();
        private readonly Transform _inactiveParent;
        private bool _isDisposed;

        /// <summary>
        /// 创建一个 GameObject 对象池。
        /// </summary>
        /// <param name="inactiveParent">
        /// 用于在 Hierarchy 中整理闲置实例的可选父级；对象池不会销毁该父级。
        /// </param>
        public MPool(Transform inactiveParent = null)
        {
            _inactiveParent = inactiveParent;
        }

        /// <inheritdoc />
        public int RegisteredCount => _entries.Count;

        /// <inheritdoc />
        public void Register(MPoolKey key, GameObject prefab, int maxInactive = 0)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (maxInactive < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInactive), "最大闲置数量不能为负数。");
            }

            if (_entries.ContainsKey(key))
            {
                throw new InvalidOperationException($"对象池 Key '{key}' 已经注册了预制体，不能重复注册。");
            }

            _entries.Add(key, new PoolEntry(this, key, prefab, maxInactive));
        }

        /// <inheritdoc />
        public bool IsRegistered(MPoolKey key)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            return _entries.ContainsKey(key);
        }

        /// <inheritdoc />
        public void Prewarm(MPoolKey key, int count)
        {
            ThrowIfDisposed();

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "预热数量不能为负数。");
            }

            PoolEntry entry = GetEntry(key);
            RepairExternallyDestroyedInactiveInstances(entry);

            if (entry.MaxInactive > 0 && count > entry.MaxInactive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"对象池 '{key}' 最多保留 {entry.MaxInactive} 个闲置实例，预热数量不能超过该上限。");
            }

            if (entry.Pool.CountInactive >= count)
            {
                return;
            }

            // 同时借出目标数量，才能让 ObjectPool 为缺少的部分调用 createFunc。
            var borrowed = new List<GameObject>(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    borrowed.Add(entry.Pool.Get());
                }
            }
            finally
            {
                for (int i = borrowed.Count - 1; i >= 0; i--)
                {
                    entry.Pool.Release(borrowed[i]);
                }
            }
        }

        /// <inheritdoc />
        public GameObject Spawn(
            MPoolKey key,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            ThrowIfDisposed();
            PoolEntry entry = GetEntry(key);
            RepairExternallyDestroyedInactiveInstances(entry);

            GameObject instance = entry.Pool.Get();
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"对象池 '{key}' 返回了已在 MPool 外部被销毁的实例。");
            }

            InstanceRecord record = GetOwnedRecord(instance);
            try
            {
                Transform instanceTransform = instance.transform;
                instanceTransform.SetParent(parent, false);
                instanceTransform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
                NotifySpawned(record);
                return instance;
            }
            catch
            {
                entry.Pool.Release(instance);
                throw;
            }
        }

        /// <inheritdoc />
        public void Despawn(GameObject instance)
        {
            ThrowIfDisposed();

            if (ReferenceEquals(instance, null))
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance == null)
            {
                throw new InvalidOperationException("该 GameObject 已在 MPool 外部被销毁，无法回收到对象池。");
            }

            InstanceRecord record = GetOwnedRecord(instance);
            if (!record.IsActive)
            {
                throw new InvalidOperationException(
                    $"GameObject '{instance.name}' 已经处于回收状态，不能重复 Despawn。");
            }

            try
            {
                NotifyDespawned(record);
            }
            finally
            {
                record.Entry.Pool.Release(instance);
            }
        }

        /// <inheritdoc />
        public void Clear(MPoolKey key)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (!_entries.TryGetValue(key, out PoolEntry entry))
            {
                return;
            }

            try
            {
                ClearEntry(entry);
            }
            finally
            {
                _entries.Remove(key);
            }
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            ThrowIfDisposed();

            PoolEntry[] entries = new PoolEntry[_entries.Count];
            _entries.Values.CopyTo(entries, 0);
            Exception firstException = null;

            for (int i = 0; i < entries.Length; i++)
            {
                try
                {
                    ClearEntry(entries[i]);
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                    {
                        firstException = exception;
                    }
                }
            }

            _entries.Clear();
            _instances.Clear();

            if (firstException != null)
            {
                throw new InvalidOperationException(
                    "清理 MPool 时有一个或多个对象池实例抛出异常。",
                    firstException);
            }
        }

        /// <summary>对象池在构造和显式注册时即已可用，因此初始化阶段无需额外工作。</summary>
        /// <param name="app">持有此对象池的框架应用。</param>
        public void Initialize(MFrameworkApp app)
        {
        }

        /// <summary>关闭模块并清理全部池实例与注册。</summary>
        public void Shutdown()
        {
            Dispose();
        }

        /// <summary>
        /// 清理全部对象池实例和注册；可以安全地重复调用。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                ClearAll();
            }
            finally
            {
                _isDisposed = true;
            }
        }

        private PoolEntry GetEntry(MPoolKey key)
        {
            ValidateKey(key);

            if (!_entries.TryGetValue(key, out PoolEntry entry))
            {
                throw new KeyNotFoundException($"对象池 Key '{key}' 尚未注册任何预制体。");
            }

            if (entry.Prefab == null)
            {
                throw new InvalidOperationException(
                    $"对象池 Key '{key}' 对应的预制体已被销毁或释放。");
            }

            return entry;
        }

        private InstanceRecord GetOwnedRecord(GameObject instance)
        {
            PoolInstanceId instanceId = GetInstanceId(instance);
            if (!_instances.TryGetValue(instanceId, out InstanceRecord record) || record.Instance != instance)
            {
                throw new InvalidOperationException(
                    $"GameObject '{instance.name}' 不属于当前 MPool 实例管理。");
            }

            return record;
        }

        private GameObject CreateInstance(PoolEntry entry)
        {
            if (entry.Prefab == null)
            {
                throw new InvalidOperationException(
                    $"对象池 Key '{entry.Key}' 对应的预制体已被销毁或释放。");
            }

            GameObject instance = Object.Instantiate(entry.Prefab);
            instance.name = entry.Prefab.name;
            instance.transform.SetParent(_inactiveParent, false);
            instance.SetActive(false);

            // 创建时一次性缓存 IMPooledGameObject 列表，避免每次 Spawn/Despawn 都调用 GetComponentsInChildren 产生 GC。
            var record = new InstanceRecord(entry, instance, instance.GetComponentsInChildren<IMPooledGameObject>(true));
            entry.InstanceIds.Add(record.InstanceId);
            _instances.Add(record.InstanceId, record);
            return instance;
        }

        private void OnGetFromUnityPool(PoolEntry entry, GameObject instance)
        {
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"对象池 '{entry.Key}' 中的闲置实例已在 MPool 外部被销毁。");
            }

            InstanceRecord record = GetOwnedRecord(instance);
            if (!ReferenceEquals(record.Entry, entry))
            {
                throw new InvalidOperationException(
                    $"GameObject '{instance.name}' 属于另一个对象池注册项。");
            }

            record.IsActive = true;
        }

        private void OnReleaseToUnityPool(PoolEntry entry, GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                return;
            }

            PoolInstanceId instanceId = GetInstanceId(instance);
            if (_instances.TryGetValue(instanceId, out InstanceRecord record))
            {
                record.IsActive = false;
            }

            if (instance == null)
            {
                // 保留归属记录，让下一次 Spawn 在 Get 前重建被外部破坏的闲置池。
                return;
            }

            instance.transform.SetParent(_inactiveParent, false);
            instance.SetActive(false);
        }

        private void DestroyUnityPoolInstance(PoolEntry entry, GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                return;
            }

            PoolInstanceId instanceId = GetInstanceId(instance);
            if (_instances.TryGetValue(instanceId, out InstanceRecord record))
            {
                RemoveRecord(record);
            }
            else
            {
                entry.InstanceIds.Remove(instanceId);
            }

            if (instance != null)
            {
                DestroyObject(instance);
            }
        }

        private void RepairExternallyDestroyedInactiveInstances(PoolEntry entry)
        {
            bool foundDestroyedInstance = false;
            foreach (PoolInstanceId instanceId in entry.InstanceIds)
            {
                if (_instances.TryGetValue(instanceId, out InstanceRecord record) &&
                    !record.IsActive &&
                    record.Instance == null)
                {
                    foundDestroyedInstance = true;
                    break;
                }
            }

            if (!foundDestroyedInstance)
            {
                return;
            }

            // ObjectPool 无法单独移除被外部 Destroy 的闲置项，因此清空闲置区并按需重新创建。
            entry.Pool.Clear();
            RemoveRemainingInactiveRecords(entry);
        }

        private void ClearEntry(PoolEntry entry)
        {
            PoolInstanceId[] ids = new PoolInstanceId[entry.InstanceIds.Count];
            entry.InstanceIds.CopyTo(ids);
            Exception firstException = null;

            // ObjectPool.Clear 只处理闲置项，活跃实例仍由 MPool 根据归属记录显式销毁。
            for (int i = 0; i < ids.Length; i++)
            {
                if (!_instances.TryGetValue(ids[i], out InstanceRecord record) || !record.IsActive)
                {
                    continue;
                }

                if (record.Instance != null)
                {
                    try
                    {
                        NotifyDespawned(record);
                    }
                    catch (Exception exception)
                    {
                        if (firstException == null)
                        {
                            firstException = exception;
                        }
                    }
                }

                DestroyTrackedInstance(record);
            }

            entry.Pool.Dispose();
            RemoveRemainingInactiveRecords(entry);
            entry.InstanceIds.Clear();

            if (firstException != null)
            {
                throw firstException;
            }
        }

        private void RemoveRemainingInactiveRecords(PoolEntry entry)
        {
            PoolInstanceId[] ids = new PoolInstanceId[entry.InstanceIds.Count];
            entry.InstanceIds.CopyTo(ids);

            for (int i = 0; i < ids.Length; i++)
            {
                if (!_instances.TryGetValue(ids[i], out InstanceRecord record) || record.IsActive)
                {
                    continue;
                }

                RemoveRecord(record);
                if (record.Instance != null)
                {
                    DestroyObject(record.Instance);
                }
            }
        }

        private void DestroyTrackedInstance(InstanceRecord record)
        {
            RemoveRecord(record);
            if (record.Instance != null)
            {
                DestroyObject(record.Instance);
            }
        }

        private void RemoveRecord(InstanceRecord record)
        {
            _instances.Remove(record.InstanceId);
            record.Entry.InstanceIds.Remove(record.InstanceId);
        }

        private static void NotifySpawned(InstanceRecord record)
        {
            IMPooledGameObject[] callbacks = record.PooledCallbacks;
            for (int i = 0; i < callbacks.Length; i++)
            {
                if (callbacks[i] is MonoBehaviour mb && mb == null)
                {
                    continue;
                }

                callbacks[i].OnSpawned();
            }
        }

        private static void NotifyDespawned(InstanceRecord record)
        {
            IMPooledGameObject[] callbacks = record.PooledCallbacks;
            for (int i = callbacks.Length - 1; i >= 0; i--)
            {
                if (callbacks[i] is MonoBehaviour mb && mb == null)
                {
                    continue;
                }

                callbacks[i].OnDespawned();
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static PoolInstanceId GetInstanceId(GameObject instance)
        {
            // Unity 6.4 起使用完整的 64 位 EntityId，保留对象标识的全部信息。
#if UNITY_6000_4_OR_NEWER
            return instance.GetEntityId();
#else
            return instance.GetInstanceID();
#endif
        }

        private static void ValidateKey(MPoolKey key)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("对象池 Key 必须包含非空字符串。", nameof(key));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MPool));
            }
        }

        private sealed class PoolEntry
        {
            public PoolEntry(MPool owner, MPoolKey key, GameObject prefab, int maxInactive)
            {
                Key = key;
                Prefab = prefab;
                MaxInactive = maxInactive;

                int maxSize = maxInactive == 0 ? int.MaxValue : maxInactive;
                int defaultCapacity = maxInactive == 0 ? 10 : Math.Min(10, maxInactive);
                Pool = new ObjectPool<GameObject>(
                    () => owner.CreateInstance(this),
                    instance => owner.OnGetFromUnityPool(this, instance),
                    instance => owner.OnReleaseToUnityPool(this, instance),
                    instance => owner.DestroyUnityPoolInstance(this, instance),
                    true,
                    defaultCapacity,
                    maxSize);
            }

            public MPoolKey Key { get; }

            public GameObject Prefab { get; }

            public int MaxInactive { get; }

            public ObjectPool<GameObject> Pool { get; }

            public HashSet<PoolInstanceId> InstanceIds { get; } = new HashSet<PoolInstanceId>();
        }

        private sealed class InstanceRecord
        {
            public InstanceRecord(PoolEntry entry, GameObject instance, IMPooledGameObject[] pooledCallbacks)
            {
                Entry = entry;
                Instance = instance;
                InstanceId = GetInstanceId(instance);
                PooledCallbacks = pooledCallbacks ?? Array.Empty<IMPooledGameObject>();
            }

            public PoolEntry Entry { get; }

            public GameObject Instance { get; }

            public PoolInstanceId InstanceId { get; }

            public IMPooledGameObject[] PooledCallbacks { get; }

            public bool IsActive { get; set; }
        }
    }
}
