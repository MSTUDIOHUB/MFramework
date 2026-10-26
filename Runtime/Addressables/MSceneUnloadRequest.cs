using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace MFramework
{
    /// <summary>
    /// 观察一次 Addressables 场景卸载；尚未激活的场景会先完成激活，再被可靠卸载。
    /// </summary>
    public sealed class MSceneUnloadRequest : IMAddressablesRequest
    {
        private readonly object _key;
        private readonly SceneInstance _sceneInstance;
        private readonly AsyncOperationHandle<SceneInstance> _sceneLoadHandle;
        private readonly UnloadSceneOptions _options;
        private AsyncOperation _activationOperation;
        private AsyncOperationHandle<SceneInstance> _unloadHandle;
        private bool _hasUnloadHandle;
        private bool _isDisposed;
        private bool _hasStarted;
        private bool _neededActivation;
        private bool _physicalCompletionRaised;
        private Action<MSceneUnloadRequest> _completed;

        internal MSceneUnloadRequest(
            object key,
            SceneInstance sceneInstance,
            AsyncOperationHandle<SceneInstance> sceneLoadHandle,
            UnloadSceneOptions options)
        {
            _key = key;
            _sceneInstance = sceneInstance;
            _sceneLoadHandle = sceneLoadHandle;
            _options = options;
            State = MAddressablesRequestState.Pending;
        }

        /// <summary>物理卸载成功或失败后触发一次；请求已被放弃时不会触发。</summary>
        public event Action<MSceneUnloadRequest> Completed
        {
            add
            {
                if (value == null || _isDisposed)
                {
                    return;
                }

                if (IsDone)
                {
                    value(this);
                    return;
                }

                _completed += value;
            }
            remove => _completed -= value;
        }

        internal event Action<MSceneUnloadRequest, bool> PhysicalCompleted;

        internal bool IsPhysicallyDone => _physicalCompletionRaised;
        internal bool PhysicalSucceeded { get; private set; }
        internal Exception PhysicalFailure => Error?.Exception;
        internal float PhysicalProgress
        {
            get
            {
                if (_physicalCompletionRaised)
                {
                    return PhysicalSucceeded ? 1f : 0f;
                }

                if (_activationOperation != null)
                {
                    return Mathf.Clamp01(_activationOperation.progress) * 0.5f;
                }

                if (_hasUnloadHandle && _unloadHandle.IsValid())
                {
                    float progress = Mathf.Clamp01(_unloadHandle.PercentComplete);
                    return _neededActivation ? 0.5f + progress * 0.5f : progress;
                }

                return 0f;
            }
        }

        /// <inheritdoc />
        public MAddressablesRequestState State { get; private set; }

        /// <inheritdoc />
        public float Progress
        {
            get
            {
                if (State == MAddressablesRequestState.Succeeded)
                {
                    return 1f;
                }

                if (State != MAddressablesRequestState.Pending)
                {
                    return 0f;
                }

                if (_activationOperation != null)
                {
                    return Mathf.Clamp01(_activationOperation.progress) * 0.5f;
                }

                if (_hasUnloadHandle && _unloadHandle.IsValid())
                {
                    float progress = Mathf.Clamp01(_unloadHandle.PercentComplete);
                    return _neededActivation ? 0.5f + progress * 0.5f : progress;
                }

                return 0f;
            }
        }

        /// <inheritdoc />
        public MAddressablesError Error { get; private set; }

        /// <inheritdoc />
        public bool IsDone => State != MAddressablesRequestState.Pending;

        /// <summary>获取此次请求是否必须先激活场景才能卸载。</summary>
        public bool NeededActivation => _neededActivation;

        /// <summary>
        /// 停止业务侧观察，但不会中断必要的激活或卸载，也不会泄漏底层句柄。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _completed = null;
            if (State == MAddressablesRequestState.Pending)
            {
                State = MAddressablesRequestState.Abandoned;
            }
        }

        internal void Start()
        {
            if (_hasStarted)
            {
                return;
            }

            _hasStarted = true;
            try
            {
                Scene scene = _sceneInstance.Scene;
                if (!_sceneLoadHandle.IsValid())
                {
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        CompleteSucceeded();
                    }
                    else
                    {
                        CompleteFailed(new InvalidOperationException(
                            "Addressables 场景加载句柄已失效，但对应 Unity 场景仍处于已加载状态。"));
                    }

                    return;
                }

                if (scene.isLoaded)
                {
                    BeginUnload();
                    return;
                }

                _neededActivation = true;
                _activationOperation = _sceneInstance.ActivateAsync();
                if (_activationOperation == null)
                {
                    CompleteFailed(new InvalidOperationException(
                        "Unity 未能为待卸载的 Addressables 场景返回有效的激活操作。"));
                    return;
                }

                if (_activationOperation.isDone)
                {
                    OnActivationCompleted(_activationOperation);
                }
                else
                {
                    _activationOperation.completed += OnActivationCompleted;
                }
            }
            catch (Exception exception)
            {
                CompleteFailed(exception);
            }
        }

        private void OnActivationCompleted(AsyncOperation operation)
        {
            operation.completed -= OnActivationCompleted;
            _activationOperation = null;

            BeginUnload();
        }

        private void BeginUnload()
        {
            try
            {
                Scene scene = _sceneInstance.Scene;
                if (!_sceneLoadHandle.IsValid())
                {
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        CompleteSucceeded();
                    }
                    else
                    {
                        CompleteFailed(new InvalidOperationException(
                            "Addressables 场景加载句柄在准备卸载时失效，但对应 Unity 场景仍处于已加载状态。"));
                    }

                    return;
                }

                if (!scene.isLoaded)
                {
                    CompleteFailed(new InvalidOperationException(
                        "场景激活操作已完成，但 Unity 仍报告该场景尚未加载。"));
                    return;
                }

                if (!HasAnotherLoadedScene(scene))
                {
                    CompleteFailed(new InvalidOperationException(
                        "MFramework 拒绝卸载最后一个已加载的 Unity 场景，请保持常驻 Bootstrap 场景处于已加载状态。"));
                    return;
                }

                _unloadHandle = Addressables.UnloadSceneAsync(_sceneLoadHandle, _options, false);
                _hasUnloadHandle = true;
                _unloadHandle.Completed += OnUnloadCompleted;
            }
            catch (Exception exception)
            {
                CompleteFailed(exception);
            }
        }

        private void OnUnloadCompleted(AsyncOperationHandle<SceneInstance> operation)
        {
            operation.Completed -= OnUnloadCompleted;

            AsyncOperationStatus status = AsyncOperationStatus.Failed;
            Exception operationException = null;
            bool sceneStillLoaded = _sceneInstance.Scene.IsValid() && _sceneInstance.Scene.isLoaded;
            try
            {
                status = operation.Status;
                operationException = operation.OperationException;
            }
            catch (Exception exception)
            {
                operationException = exception;
            }
            finally
            {
                if (operation.IsValid())
                {
                    Addressables.Release(operation);
                }

                _hasUnloadHandle = false;
            }

            if (status == AsyncOperationStatus.Succeeded && !sceneStillLoaded)
            {
                CompleteSucceeded();
                return;
            }

            CompleteFailed(operationException ?? new InvalidOperationException(
                sceneStillLoaded
                    ? "Addressables 报告场景卸载已完成，但该 Unity 场景仍处于已加载状态。"
                    : "Addressables 场景卸载操作失败。"));
        }

        private void CompleteSucceeded()
        {
            bool abandoned = _isDisposed || State == MAddressablesRequestState.Abandoned;
            if (!abandoned)
            {
                State = MAddressablesRequestState.Succeeded;
            }

            RaisePhysicalCompletion(true);
            if (!abandoned)
            {
                _completed?.Invoke(this);
            }
        }

        private void CompleteFailed(Exception exception)
        {
            Error = new MAddressablesError(_key, exception?.Message, exception);
            bool abandoned = _isDisposed || State == MAddressablesRequestState.Abandoned;
            if (!abandoned)
            {
                State = MAddressablesRequestState.Failed;
            }

            RaisePhysicalCompletion(false);
            if (!abandoned)
            {
                _completed?.Invoke(this);
            }
        }

        private void RaisePhysicalCompletion(bool succeeded)
        {
            if (_physicalCompletionRaised)
            {
                return;
            }

            _physicalCompletionRaised = true;
            PhysicalSucceeded = succeeded;
            PhysicalCompleted?.Invoke(this, succeeded);
            PhysicalCompleted = null;
        }

        private static bool HasAnotherLoadedScene(Scene sceneToUnload)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate != sceneToUnload && candidate.IsValid() && candidate.isLoaded)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
