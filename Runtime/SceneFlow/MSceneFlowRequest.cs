using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>观察、诊断或取消一次由 <see cref="MSceneFlow"/> 执行的场景切换。</summary>
    public sealed class MSceneFlowRequest : IDisposable
    {
        private MSceneFlow _owner;
        private float _progress;
        private bool _cancellationRequested;
        private Action<MSceneFlowRequest> _completed;

        internal MSceneFlowRequest(MSceneFlow owner, MSceneFlowPlan plan, IMSceneFlowScene previousScene)
        {
            _owner = owner;
            Plan = plan;
            PreviousScene = previousScene;
            State = MSceneFlowState.Running;
            Stage = MSceneFlowStage.None;
        }

        /// <summary>当前阶段发生改变后触发；观察者异常只会写入 Unity Console。</summary>
        public event Action<MSceneFlowRequest> StageChanged;

        /// <summary>同阶段中的一个用户命令即将启动时触发。</summary>
        public event Action<MSceneFlowRequest> CommandStarted;

        /// <summary>请求进入 Succeeded、Failed 或 Cancelled 后触发一次。</summary>
        public event Action<MSceneFlowRequest> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (IsDone)
                {
                    InvokeSafely(value);
                    return;
                }

                _completed += value;
            }
            remove => _completed -= value;
        }

        /// <summary>获取此次切换使用的冻结计划。</summary>
        public MSceneFlowPlan Plan { get; }

        /// <summary>获取请求当前状态。</summary>
        public MSceneFlowState State { get; private set; }

        /// <summary>获取当前阶段。</summary>
        public MSceneFlowStage Stage { get; private set; }

        /// <summary>获取当前正在执行的用户命令名称；没有命令时为 null。</summary>
        public string CurrentCommandName { get; private set; }

        /// <summary>获取切换开始时的旧场景；首次加载时为 null。</summary>
        public IMSceneFlowScene PreviousScene { get; }

        /// <summary>
        /// 获取已经加载的目标场景；加载完成前为 null。所有权属于 MSceneFlow，调用方不得释放。
        /// </summary>
        public IMSceneFlowScene TargetScene { get; internal set; }

        /// <summary>获取从 0 到 1 且不会倒退的近似进度。</summary>
        public float Progress => _progress;

        /// <summary>获取导致失败的异常；未失败时为 null。</summary>
        public Exception Failure { get; internal set; }

        /// <summary>获取首次发生错误的阶段；未失败时为 None。</summary>
        public MSceneFlowStage FailedStage { get; internal set; }

        /// <summary>
        /// 获取切换是否已经越过不可恢复边界。为 true 时旧场景可能已卸载，失败或取消不会销毁可用的新当前场景。
        /// </summary>
        public bool HasCommitted { get; internal set; }

        /// <summary>获取调用方是否已经请求取消。</summary>
        public bool IsCancellationRequested => _cancellationRequested;

        /// <summary>获取请求是否已经进入终态。</summary>
        public bool IsDone => State == MSceneFlowState.Succeeded
            || State == MSceneFlowState.Failed
            || State == MSceneFlowState.Cancelled;

        /// <summary>
        /// 请求取消。普通阶段的用户命令会立即取消；Unity/Addressables 操作会先安全完成再收口。
        /// 为保证释放输入锁等清理不被截断，已经开始的 Finally 命令只会被 MSceneFlow.Shutdown 强制终止。
        /// </summary>
        /// <returns>本次调用是否首次提交了取消请求。</returns>
        public bool Cancel()
        {
            return _owner != null && _owner.Cancel(this);
        }

        /// <summary>等同于 <see cref="Cancel"/>。</summary>
        public void Dispose()
        {
            Cancel();
        }

        internal void MarkCancelling()
        {
            _cancellationRequested = true;
            if (!IsDone)
            {
                State = MSceneFlowState.Cancelling;
            }
        }

        internal void SetStage(MSceneFlowStage stage)
        {
            Stage = stage;
            CurrentCommandName = null;
            InvokeSafely(StageChanged);
        }

        internal void SetCurrentCommand(string name)
        {
            CurrentCommandName = name;
            InvokeSafely(CommandStarted);
        }

        internal void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (value > _progress)
            {
                _progress = value;
            }
        }

        internal void Finish(MSceneFlowState state, MSceneFlowStage stage)
        {
            State = state;
            Stage = stage;
            CurrentCommandName = null;
            if (state == MSceneFlowState.Succeeded)
            {
                _progress = 1f;
            }

            _owner = null;
            InvokeSafely(StageChanged);
            Action<MSceneFlowRequest> completed = _completed;
            _completed = null;
            InvokeSafely(completed);
        }

        private void InvokeSafely(Action<MSceneFlowRequest> handlers)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<MSceneFlowRequest>)invocationList[i])(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
