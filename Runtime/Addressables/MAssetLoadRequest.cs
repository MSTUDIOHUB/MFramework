using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MFramework
{
    /// <summary>观察一次 Addressables 异步资源加载，并持有成功后得到的租约。</summary>
    /// <typeparam name="TAsset">Unity 资源类型。</typeparam>
    public sealed class MAssetLoadRequest<TAsset> : IMAddressablesRequest
        where TAsset : UnityEngine.Object
    {
        private readonly object _key;
        private AsyncOperationHandle<TAsset> _handle;
        private bool _isDisposed;

        internal MAssetLoadRequest(object key, AsyncOperationHandle<TAsset> handle, MAssetScope scope)
        {
            _key = key;
            _handle = handle;
            State = MAddressablesRequestState.Pending;
            _handle.Completed += OnCompleted;
            scope?.Own(this);
        }

        /// <summary>加载成功或失败后触发一次；请求已被放弃时不会触发。</summary>
        public event Action<MAssetLoadRequest<TAsset>> Completed;

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

        /// <summary>获取成功后的资源租约；请求成功前为 null。</summary>
        public MAssetLease<TAsset> Lease { get; private set; }

        /// <summary>停止后续回调，并释放稍后得到的结果或已经成功的租约。</summary>
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

        private void OnCompleted(AsyncOperationHandle<TAsset> operation)
        {
            _handle.Completed -= OnCompleted;

            if (operation.Status == AsyncOperationStatus.Succeeded)
            {
                Lease = new MAssetLease<TAsset>(operation);
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
