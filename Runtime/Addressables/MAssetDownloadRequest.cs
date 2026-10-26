using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MFramework
{
    /// <summary>观察某个 Addressables 地址或标签所依赖资源的下载过程。</summary>
    public sealed class MAssetDownloadRequest : IMAddressablesRequest
    {
        private readonly object _key;
        private AsyncOperationHandle _handle;
        private bool _isDisposed;

        internal MAssetDownloadRequest(object key, AsyncOperationHandle handle, MAssetScope scope)
        {
            _key = key;
            _handle = handle;
            State = MAddressablesRequestState.Pending;
            _handle.Completed += OnCompleted;
            scope?.Own(this);
        }

        /// <summary>下载成功或失败后触发；请求已被放弃时不会触发。</summary>
        public event Action<MAssetDownloadRequest> Completed;

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

        /// <summary>停止后续回调；已经开始的下载仍由 Addressables 完成并清理。</summary>
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
            }
        }

        private void OnCompleted(AsyncOperationHandle operation)
        {
            _handle.Completed -= OnCompleted;
            bool succeeded = operation.Status == AsyncOperationStatus.Succeeded;
            if (!succeeded)
            {
                Error = new MAddressablesError(
                    _key,
                    operation.OperationException?.Message,
                    operation.OperationException);
            }

            if (operation.IsValid())
            {
                Addressables.Release(operation);
            }

            if (_isDisposed || State == MAddressablesRequestState.Abandoned)
            {
                return;
            }

            State = succeeded
                ? MAddressablesRequestState.Succeeded
                : MAddressablesRequestState.Failed;
            Completed?.Invoke(this);
        }
    }
}
