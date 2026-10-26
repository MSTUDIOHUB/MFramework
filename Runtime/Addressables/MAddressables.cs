using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace MFramework
{
    /// <summary>
    /// 在不隐藏 Addressables 运行时键模型的前提下，提供轻量且生命周期安全的辅助 API。
    /// </summary>
    /// <remarks>
    /// 此封装补充了明确的资源归属、加载进度、可读错误和停止回调能力，同时刻意在公开 API 中
    /// 保留 Addressables 的 AssetReference 与键，使官方文档和已有知识仍然适用。
    /// </remarks>
    public static class MAddressables
    {
        /// <summary>通过地址、可作为键使用的标签或运行时键加载资源。</summary>
        /// <typeparam name="TAsset">期望得到的 Unity 资源类型。</typeparam>
        /// <param name="key">Addressables 键。</param>
        /// <param name="scope">可选的生命周期作用域，用来释放请求及其结果。</param>
        /// <returns>一个加载请求；其 <see cref="MAssetLoadRequest{TAsset}.Lease"/> 持有加载后的资源。</returns>
        public static MAssetLoadRequest<TAsset> LoadAssetAsync<TAsset>(object key, MAssetScope scope = null)
            where TAsset : UnityEngine.Object
        {
            EnsureValidKey(key);
            AsyncOperationHandle<TAsset> handle = Addressables.LoadAssetAsync<TAsset>(key);
            return new MAssetLoadRequest<TAsset>(key, handle, scope);
        }

        /// <summary>通过可在 Inspector 中配置的 <see cref="AssetReference"/> 加载资源。</summary>
        /// <typeparam name="TAsset">期望得到的 Unity 资源类型。</typeparam>
        /// <param name="reference">已经配置的资源引用。</param>
        /// <param name="scope">可选的生命周期作用域，用来释放请求及其结果。</param>
        /// <returns>一个由租约持有资源的加载请求。</returns>
        public static MAssetLoadRequest<TAsset> LoadAssetAsync<TAsset>(AssetReference reference, MAssetScope scope = null)
            where TAsset : UnityEngine.Object
        {
            object key = GetRuntimeKey(reference);
            return LoadAssetAsync<TAsset>(key, scope);
        }

        /// <summary>实例化一个 Addressables 预制体，并可指定父节点。</summary>
        /// <param name="key">预制体地址或运行时键。</param>
        /// <param name="parent">可选的父级 Transform。</param>
        /// <param name="instantiateInWorldSpace">设置父节点时是否保持世界坐标。</param>
        /// <param name="scope">可选的生命周期作用域，用来释放实例。</param>
        /// <returns>一个由租约持有实例的请求。</returns>
        public static MGameObjectInstantiateRequest InstantiateAsync(
            object key,
            Transform parent = null,
            bool instantiateInWorldSpace = false,
            MAssetScope scope = null)
        {
            EnsureValidKey(key);
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(
                key,
                parent,
                instantiateInWorldSpace,
                true);
            return new MGameObjectInstantiateRequest(key, handle, scope);
        }

        /// <summary>实例化一个可在 Inspector 中配置的 Addressables 预制体引用。</summary>
        /// <param name="reference">已经配置的预制体引用。</param>
        /// <param name="parent">可选的父级 Transform。</param>
        /// <param name="instantiateInWorldSpace">设置父节点时是否保持世界坐标。</param>
        /// <param name="scope">可选的生命周期作用域，用来释放实例。</param>
        /// <returns>一个由租约持有实例的请求。</returns>
        public static MGameObjectInstantiateRequest InstantiateAsync(
            AssetReference reference,
            Transform parent = null,
            bool instantiateInWorldSpace = false,
            MAssetScope scope = null)
        {
            object key = GetRuntimeKey(reference);
            return InstantiateAsync(key, parent, instantiateInWorldSpace, scope);
        }

        /// <summary>通过 Addressables 加载场景。</summary>
        /// <param name="key">场景地址或运行时键。</param>
        /// <param name="loadMode">场景替换当前场景，还是以附加方式加入。</param>
        /// <param name="activateOnLoad">Addressables 是否在加载后立即激活场景。</param>
        /// <param name="priority">Unity 异步操作优先级。</param>
        /// <param name="scope">可选的生命周期作用域，用来卸载场景。</param>
        /// <returns>一个由租约持有已加载场景的请求。</returns>
        public static MSceneLoadRequest LoadSceneAsync(
            object key,
            LoadSceneMode loadMode = LoadSceneMode.Single,
            bool activateOnLoad = true,
            int priority = 100,
            MAssetScope scope = null)
        {
            EnsureValidKey(key);
            if (loadMode == LoadSceneMode.Single && !activateOnLoad)
            {
                throw new ArgumentException(
                    "延迟激活的场景必须使用 LoadSceneMode.Additive，以便安全激活与卸载。",
                    nameof(loadMode));
            }

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
                key,
                loadMode,
                activateOnLoad,
                priority);
            return new MSceneLoadRequest(key, handle, scope);
        }

        /// <summary>通过可在 Inspector 中配置的 Addressables 引用加载场景。</summary>
        /// <param name="reference">已经配置的场景引用。</param>
        /// <param name="loadMode">场景替换当前场景，还是以附加方式加入。</param>
        /// <param name="activateOnLoad">Addressables 是否在加载后立即激活场景。</param>
        /// <param name="priority">Unity 异步操作优先级。</param>
        /// <param name="scope">可选的生命周期作用域，用来卸载场景。</param>
        /// <returns>一个由租约持有已加载场景的请求。</returns>
        public static MSceneLoadRequest LoadSceneAsync(
            AssetReference reference,
            LoadSceneMode loadMode = LoadSceneMode.Single,
            bool activateOnLoad = true,
            int priority = 100,
            MAssetScope scope = null)
        {
            object key = GetRuntimeKey(reference);
            return LoadSceneAsync(key, loadMode, activateOnLoad, priority, scope);
        }

        /// <summary>下载某个地址或标签依赖的全部资源。</summary>
        /// <param name="key">Addressables 地址、标签或运行时键。</param>
        /// <param name="scope">可选的生命周期作用域，用来阻止过期回调。</param>
        /// <returns>一个可以查询进度的下载请求。</returns>
        public static MAssetDownloadRequest DownloadDependenciesAsync(object key, MAssetScope scope = null)
        {
            EnsureValidKey(key);
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(key, false);
            return new MAssetDownloadRequest(key, handle, scope);
        }

        /// <summary>下载一个可在 Inspector 中配置的 Addressables 引用所依赖的资源。</summary>
        /// <param name="reference">已经配置的资源引用。</param>
        /// <param name="scope">可选的生命周期作用域，用来阻止过期回调。</param>
        /// <returns>一个可以查询进度的下载请求。</returns>
        public static MAssetDownloadRequest DownloadDependenciesAsync(
            AssetReference reference,
            MAssetScope scope = null)
        {
            object key = GetRuntimeKey(reference);
            return DownloadDependenciesAsync(key, scope);
        }

        private static object GetRuntimeKey(AssetReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (!reference.RuntimeKeyIsValid())
            {
                throw new ArgumentException("传入的 AssetReference 不包含有效的运行时键。", nameof(reference));
            }

            return reference.RuntimeKey;
        }

        private static void EnsureValidKey(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string textKey = key as string;
            if (textKey != null && string.IsNullOrWhiteSpace(textKey))
            {
                throw new ArgumentException("Addressables 字符串键不能为空或仅包含空白字符。", nameof(key));
            }
        }
    }
}
