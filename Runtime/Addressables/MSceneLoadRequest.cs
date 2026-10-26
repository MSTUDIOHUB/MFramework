using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace MFramework
{
    /// <summary>观察一次 Addressables 场景加载，并持有成功加载的场景。</summary>
    public sealed class MSceneLoadRequest : IMAddressablesRequest
    {
        private readonly object _key;
        private AsyncOperationHandle<SceneInstance> _handle;
        private bool _isDisposed;

        internal MSceneLoadRequest(object key, AsyncOperationHandle<SceneInstance> handle, MAssetScope scope)
        {
            _key = key;
            _handle = handle;
            State = MAddressablesRequestState.Pending;
            _handle.Completed += OnCompleted;
            scope?.Own(this);
        }

        /// <summary>场景加载成功或失败后触发一次；请求已被放弃时不会触发。</summary>
        public event Action<MSceneLoadRequest> Completed;

        /// <inheritdoc />
        public MAddressablesRequestState State { get; private set; }

        /// <inheritdoc />
        public float Progress => State == MAddressablesRequestState.Pending && _handle.IsValid()
            ? _handle.PercentComplete
            : (State == MAddressablesRequestState.Succeeded ? 1f : 0f);

        /// <inheritdoc />
        public MAddressablesError Error { get; private set; }

        /// <inheritdoc />
        public bool IsDone => State != MAddressablesRequestState.Pending;

        /// <summary>获取成功后的场景租约；请求成功前为 null。</summary>
        public MSceneLease Lease { get; private set; }

        /// <summary>
        /// 把成功结果的所有权从请求转移给调用方。
        /// </summary>
        /// <remarks>
        /// 转移后 <see cref="Lease"/> 变为 null，释放本请求不会再卸载该场景。
        /// </remarks>
        /// <returns>由调用方负责卸载的场景租约。</returns>
        /// <exception cref="InvalidOperationException">请求尚未成功，或租约已经被转移。</exception>
        public MSceneLease TakeLease()
        {
            if (State != MAddressablesRequestState.Succeeded)
            {
                throw new InvalidOperationException("只能在场景加载成功后转移场景租约（TakeLease）。");
            }

            if (Lease == null)
            {
                throw new InvalidOperationException("该场景租约已经被转移，不能重复获取。");
            }

            MSceneLease lease = Lease;
            Lease = null;
            return lease;
        }

        /// <summary>停止后续回调，并卸载稍后得到的结果或已经成功的场景。</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Completed = null;
            if (State == MAddressablesRequestState.Pending)
            {
                State = MAddressablesRequestState.Abandoned;
                return;
            }

            Lease?.Dispose();
        }

        private void OnCompleted(AsyncOperationHandle<SceneInstance> operation)
        {
            _handle.Completed -= OnCompleted;
            if (operation.Status == AsyncOperationStatus.Succeeded)
            {
                Lease = new MSceneLease(_key, operation);
                if (_isDisposed || State == MAddressablesRequestState.Abandoned)
                {
                    Lease.Dispose();
                    return;
                }

                State = MAddressablesRequestState.Succeeded;
            }
            else
            {
                Error = new MAddressablesError(
                    _key,
                    operation.OperationException?.Message,
                    operation.OperationException);

                if (operation.IsValid())
                {
                    Addressables.Release(operation);
                }

                if (_isDisposed || State == MAddressablesRequestState.Abandoned)
                {
                    return;
                }

                State = MAddressablesRequestState.Failed;
            }

            Completed?.Invoke(this);
        }
    }
}
