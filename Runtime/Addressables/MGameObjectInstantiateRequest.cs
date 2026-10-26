using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MFramework
{
    /// <summary>观察一次 Addressables 预制体实例化，并持有创建出的实例。</summary>
    public sealed class MGameObjectInstantiateRequest : IMAddressablesRequest
    {
        private readonly object _key;
        private AsyncOperationHandle<GameObject> _handle;
        private bool _isDisposed;

        internal MGameObjectInstantiateRequest(object key, AsyncOperationHandle<GameObject> handle, MAssetScope scope)
        {
            _key = key;
            _handle = handle;
            State = MAddressablesRequestState.Pending;
            _handle.Completed += OnCompleted;
            scope?.Own(this);
        }

        /// <summary>实例化成功或失败后触发一次；请求已被放弃时不会触发。</summary>
        public event Action<MGameObjectInstantiateRequest> Completed;

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

        /// <summary>获取成功后的实例租约；请求成功前为 null。</summary>
        public MGameObjectInstanceLease Lease { get; private set; }

        /// <summary>停止后续回调，并释放稍后得到的结果或已经成功的实例。</summary>
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

        private void OnCompleted(AsyncOperationHandle<GameObject> operation)
        {
            _handle.Completed -= OnCompleted;
            if (operation.Status == AsyncOperationStatus.Succeeded)
            {
                Lease = new MGameObjectInstanceLease(operation);
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
