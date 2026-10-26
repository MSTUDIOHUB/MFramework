using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace MFramework
{
    /// <summary>
    /// 使用 MAddressables、Additive 加载和显式激活实现的默认 MSceneFlow 后端。
    /// </summary>
    public sealed class MAddressablesSceneFlowBackend : IMSceneFlowBackend
    {
        private readonly Scene _bootstrapScene;

        /// <summary>
        /// 创建后端，并把创建当下的 Active Scene 记作优先 Bootstrap 候选。
        /// </summary>
        public MAddressablesSceneFlowBackend()
        {
            _bootstrapScene = SceneManager.GetActiveScene();
        }

        /// <inheritdoc />
        public IMSceneFlowLoadOperation LoadAsync(object key, int priority)
        {
            MSceneLoadRequest request;
            AssetReference reference = key as AssetReference;
            if (reference != null)
            {
                request = MAddressables.LoadSceneAsync(
                    reference,
                    LoadSceneMode.Additive,
                    false,
                    priority);
            }
            else
            {
                request = MAddressables.LoadSceneAsync(
                    key,
                    LoadSceneMode.Additive,
                    false,
                    priority);
            }

            return new AddressablesLoadOperation(request);
        }

        /// <inheritdoc />
        public IMSceneFlowOperation ActivateAsync(IMSceneFlowScene scene)
        {
            MSceneLease lease = RequireLease(scene);
            return new SceneActivationOperation(lease, lease.ActivateAsync());
        }

        /// <inheritdoc />
        public IMSceneFlowOperation UnloadAsync(IMSceneFlowScene scene)
        {
            MSceneLease lease = RequireLease(scene);
            EnsureAnotherSceneIsActive(lease.Scene);
            MSceneUnloadRequest request = lease.UnloadAsync();
            return new AddressablesUnloadOperation(request);
        }

        /// <inheritdoc />
        public void SetActiveScene(IMSceneFlowScene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            Scene unityScene = scene.Scene;
            if (!unityScene.IsValid() || !unityScene.isLoaded)
            {
                throw new InvalidOperationException("只有有效且已激活的场景才能设为 Unity Active Scene。");
            }

            if (!SceneManager.SetActiveScene(unityScene))
            {
                throw new InvalidOperationException("Unity 拒绝将该目标场景设为 Active Scene。");
            }
        }

        private static MSceneLease RequireLease(IMSceneFlowScene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            MSceneLease lease = scene as MSceneLease;
            if (lease == null)
            {
                throw new ArgumentException(
                    "MAddressablesSceneFlowBackend 只能操作 MSceneLease 类型的场景实例。",
                    nameof(scene));
            }

            return lease;
        }

        private void EnsureAnotherSceneIsActive(Scene sceneToUnload)
        {
            if (SceneManager.GetActiveScene() != sceneToUnload)
            {
                return;
            }

            if (_bootstrapScene.IsValid()
                && _bootstrapScene.isLoaded
                && _bootstrapScene != sceneToUnload)
            {
                if (!SceneManager.SetActiveScene(_bootstrapScene))
                {
                    throw new InvalidOperationException(
                        "在卸载内容场景前，Unity 拒绝将 Bootstrap 场景切换为 Active Scene。");
                }

                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.IsValid() && candidate.isLoaded && candidate != sceneToUnload)
                {
                    if (!SceneManager.SetActiveScene(candidate))
                    {
                        throw new InvalidOperationException(
                            "在卸载内容场景前，Unity 拒绝将其他已加载场景切换为 Active Scene。");
                    }

                    return;
                }
            }
        }

        private static Exception ToException(MAddressablesError error, string fallback)
        {
            if (error == null)
            {
                return new InvalidOperationException(fallback);
            }

            return error.Exception ?? new InvalidOperationException(error.ToString());
        }

        private sealed class AddressablesLoadOperation : IMSceneFlowLoadOperation
        {
            private MSceneLoadRequest _request;

            public AddressablesLoadOperation(MSceneLoadRequest request)
            {
                _request = request ?? throw new ArgumentNullException(nameof(request));
            }

            public bool IsDone => _request == null || _request.IsDone;

            public float Progress => _request == null ? 1f : _request.Progress;

            public Exception Failure => _request != null && _request.State == MAddressablesRequestState.Failed
                ? ToException(_request.Error, "Addressables 场景加载操作失败。")
                : null;

            public IMSceneFlowScene TakeScene()
            {
                if (_request == null)
                {
                    throw new InvalidOperationException("该加载操作不再持有有效的请求实例。");
                }

                MSceneLease lease = _request.TakeLease();
                _request.Dispose();
                _request = null;
                return lease;
            }

            public void Dispose()
            {
                _request?.Dispose();
                _request = null;
            }
        }

        private sealed class AddressablesUnloadOperation : IMSceneFlowOperation
        {
            private MSceneUnloadRequest _request;

            public AddressablesUnloadOperation(MSceneUnloadRequest request)
            {
                _request = request ?? throw new ArgumentNullException(nameof(request));
            }

            public bool IsDone => _request == null || _request.IsPhysicallyDone;

            public float Progress => _request == null ? 1f : _request.PhysicalProgress;

            public Exception Failure
            {
                get
                {
                    if (_request == null || !_request.IsPhysicallyDone)
                    {
                        return null;
                    }

                    return !_request.PhysicalSucceeded
                        ? (_request.PhysicalFailure
                            ?? ToException(_request.Error, "Addressables 场景卸载操作失败。"))
                        : null;
                }
            }

            public void Dispose()
            {
                _request?.Dispose();
                _request = null;
            }
        }

        private sealed class SceneActivationOperation : IMSceneFlowOperation
        {
            private readonly MSceneLease _lease;
            private AsyncOperation _operation;

            public SceneActivationOperation(MSceneLease lease, AsyncOperation operation)
            {
                _lease = lease ?? throw new ArgumentNullException(nameof(lease));
                _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            }

            public bool IsDone => _operation == null || _operation.isDone;

            public float Progress => _operation == null ? 1f : Mathf.Clamp01(_operation.progress);

            public Exception Failure
            {
                get
                {
                    if (_operation == null || !_operation.isDone || _lease.IsActivated)
                    {
                        return null;
                    }

                    return new InvalidOperationException(
                        "Unity 报告场景激活操作已完成，但该场景仍未处于已加载状态。");
                }
            }

            public void Dispose()
            {
                _operation = null;
            }
        }
    }
}
