using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace MFramework
{
    /// <summary>持有一个由 Addressables 返回的资源句柄。</summary>
    /// <typeparam name="TAsset">Unity 资源类型。</typeparam>
    public sealed class MAssetLease<TAsset> : IDisposable
        where TAsset : UnityEngine.Object
    {
        private AsyncOperationHandle<TAsset> _handle;

        internal MAssetLease(AsyncOperationHandle<TAsset> handle)
        {
            _handle = handle;
            Asset = handle.Result;
        }

        /// <summary>获取已经加载的资源。</summary>
        public TAsset Asset { get; }

        /// <summary>获取 Addressables 句柄是否已经释放。</summary>
        public bool IsReleased { get; private set; }

        /// <summary>释放资源句柄；重复调用是安全的。</summary>
        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }
        }
    }

    /// <summary>持有一个通过 Addressables 实例化的 GameObject。</summary>
    public sealed class MGameObjectInstanceLease : IDisposable
    {
        private AsyncOperationHandle<GameObject> _handle;

        internal MGameObjectInstanceLease(AsyncOperationHandle<GameObject> handle)
        {
            _handle = handle;
            Instance = handle.Result;
        }

        /// <summary>获取已经实例化的 GameObject。</summary>
        public GameObject Instance { get; }

        /// <summary>获取实例是否已经交还给 Addressables。</summary>
        public bool IsReleased { get; private set; }

        /// <summary>通过 Addressables 释放并销毁实例。</summary>
        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            if (_handle.IsValid())
            {
                Addressables.ReleaseInstance(_handle);
            }
        }
    }

    /// <summary>持有一个通过 Addressables 加载的场景。</summary>
    public sealed class MSceneLease : IMSceneFlowScene
    {
        private readonly object _key;
        private AsyncOperationHandle<SceneInstance> _handle;
        private MSceneUnloadRequest _unloadRequest;
        private bool _isUnloadRequested;

        internal MSceneLease(object key, AsyncOperationHandle<SceneInstance> handle)
        {
            _key = key;
            _handle = handle;
            SceneInstance = handle.Result;
        }

        /// <summary>获取加载该场景时使用的 Addressables 键。</summary>
        public object Key => _key;

        /// <summary>获取已加载的 Addressables 场景实例。</summary>
        public SceneInstance SceneInstance { get; }

        /// <summary>获取底层 Unity 场景。</summary>
        public Scene Scene => SceneInstance.Scene;

        /// <summary>获取场景是否已经完成 Unity 激活。</summary>
        public bool IsActivated => SceneInstance.Scene.isLoaded;

        /// <summary>获取场景所有权是否已经成功交还给 Addressables。</summary>
        public bool IsReleased { get; private set; }

        /// <summary>获取当前是否已有物理卸载正在进行。</summary>
        public bool IsUnloadRequested => _isUnloadRequested;

        /// <summary>当场景以“暂不激活”方式加载时，开始激活场景。</summary>
        /// <returns>Unity 的异步激活操作。</returns>
        public AsyncOperation ActivateAsync()
        {
            if (IsReleased || _isUnloadRequested)
            {
                throw new InvalidOperationException("场景已请求卸载或已释放，不能再次激活。");
            }

            return SceneInstance.ActivateAsync();
        }

        /// <summary>
        /// 开始卸载场景，并返回可观察进度、错误与完成状态的请求。
        /// </summary>
        /// <remarks>
        /// Addressables 不能直接卸载尚未激活的场景，因此请求会在必要时先完成激活，再执行卸载。
        /// 第一次调用提供的 <paramref name="options"/> 生效；后续调用复用同一个请求。
        /// </remarks>
        /// <param name="options">Unity 场景卸载选项。</param>
        /// <returns>唯一的场景卸载请求。</returns>
        public MSceneUnloadRequest UnloadAsync(UnloadSceneOptions options = UnloadSceneOptions.None)
        {
            if (_unloadRequest != null)
            {
                return _unloadRequest;
            }

            MSceneUnloadRequest request = new MSceneUnloadRequest(_key, SceneInstance, _handle, options);
            _unloadRequest = request;
            _isUnloadRequested = true;
            request.PhysicalCompleted += OnPhysicalUnloadCompleted;
            request.Start();
            return request;
        }

        /// <summary>
        /// 开始卸载场景。调用方若需要等待物理卸载完成，应改用 <see cref="UnloadAsync"/>。
        /// </summary>
        public void Dispose()
        {
            if (!IsReleased)
            {
                UnloadAsync();
            }
        }

        private void OnPhysicalUnloadCompleted(MSceneUnloadRequest request, bool succeeded)
        {
            request.PhysicalCompleted -= OnPhysicalUnloadCompleted;
            _isUnloadRequested = false;
            if (succeeded)
            {
                IsReleased = true;
                return;
            }

            IsReleased = false;
            if (ReferenceEquals(_unloadRequest, request))
            {
                _unloadRequest = null;
            }
        }
    }
}
