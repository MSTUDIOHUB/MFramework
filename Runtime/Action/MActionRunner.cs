using System;
using System.Collections.Generic;
using UnityObject = UnityEngine.Object;

namespace MFramework
{
    /// <summary>
    /// 持有并推进根动作节点。
    /// </summary>
    /// <remarks>
    /// 请向 <see cref="MFrameworkApp"/> 注册一个运行器。在某次 Tick 期间启动的动作会立即开始，
    /// 但要到下一帧才会首次获得时间更新。
    /// </remarks>
    public sealed class MActionRunner : IMFrameworkModule, IMFrameworkTickable, IDisposable
    {
        private readonly List<RunningAction> _runningActions = new List<RunningAction>();
        private readonly List<RunningAction> _tickBuffer = new List<RunningAction>();
        private bool _isTicking;
        private bool _isShutdown;

        /// <summary>获取该运行器当前持有的根动作数量。</summary>
        public int ActiveCount => _runningActions.Count;

        /// <summary>
        /// 启动一个根动作。
        /// </summary>
        /// <param name="node">新创建的单次使用动作节点。</param>
        /// <param name="owner">
        /// 可选的 Unity 对象。该对象销毁后，动作会在运行器下一次 Tick 时被取消。
        /// </param>
        /// <param name="timeMode">推进该动作时使用的帧时间模式。</param>
        /// <returns>用于观察或取消动作的句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="node"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">运行器已经关闭，或节点不是新创建的节点。</exception>
        public MActionHandle Run(
            IMActionNode node,
            UnityObject owner = null,
            MTimeMode timeMode = MTimeMode.Scaled)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (_isShutdown)
            {
                throw new InvalidOperationException("MActionRunner 已关闭，不能启动新动作。");
            }

            if (node.Status != MActionStatus.Created)
            {
                throw new InvalidOperationException("MActionRunner 要求传入尚未启动的新动作节点（Created 状态）。");
            }

            RunningAction runningAction = new RunningAction(
                node,
                owner,
                !ReferenceEquals(owner, null),
                timeMode);
            MActionHandle handle = new MActionHandle(this, runningAction);

            node.Start();
            if (!IsTerminal(node.Status))
            {
                runningAction.IsRegistered = true;
                _runningActions.Add(runningAction);
            }

            return handle;
        }

        /// <inheritdoc/>
        public void Initialize(MFrameworkApp app)
        {
            _isShutdown = false;
        }

        /// <inheritdoc/>
        public void Tick(MFrameTime time)
        {
            Tick(time.DeltaTime, time.UnscaledDeltaTime);
        }

        /// <summary>
        /// 使用明确给出的缩放与非缩放帧时间，推进所有正在运行的动作。
        /// </summary>
        /// <param name="scaledDeltaTime">缩放后的帧间隔，单位为秒。</param>
        /// <param name="unscaledDeltaTime">未缩放的帧间隔，单位为秒。</param>
        /// <exception cref="ArgumentOutOfRangeException">任意一个帧间隔为负数。</exception>
        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (scaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime), "缩放帧时间不能为负数。");
            }

            if (unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime), "非缩放帧时间不能为负数。");
            }

            if (_isShutdown || _runningActions.Count == 0)
            {
                return;
            }

            // 复用 _tickBuffer 避免每帧 ToArray() 产生 GC 分配；若发生重入则退回临时列表。
            bool useSharedBuffer = !_isTicking;
            List<RunningAction> snapshot;
            if (useSharedBuffer)
            {
                _isTicking = true;
                _tickBuffer.Clear();
                _tickBuffer.AddRange(_runningActions);
                snapshot = _tickBuffer;
            }
            else
            {
                snapshot = new List<RunningAction>(_runningActions);
            }

            try
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    RunningAction runningAction = snapshot[i];
                    if (!runningAction.IsRegistered)
                    {
                        continue;
                    }

                    if (runningAction.HasOwner && runningAction.Owner == null)
                    {
                        Cancel(runningAction);
                        continue;
                    }

                    float deltaTime = runningAction.TimeMode == MTimeMode.Unscaled
                        ? unscaledDeltaTime
                        : scaledDeltaTime;

                    try
                    {
                        runningAction.Node.Tick(deltaTime);
                    }
                    finally
                    {
                        if (IsTerminal(runningAction.Node.Status))
                        {
                            Remove(runningAction);
                        }
                    }
                }
            }
            finally
            {
                if (useSharedBuffer)
                {
                    _tickBuffer.Clear();
                    _isTicking = false;
                }
            }
        }

        /// <summary>取消所有正在运行的根动作。</summary>
        /// <exception cref="AggregateException">一个或多个取消回调执行失败。</exception>
        public void CancelAll()
        {
            if (_runningActions.Count == 0)
            {
                return;
            }

            RunningAction[] snapshot = _runningActions.ToArray();
            List<Exception> exceptions = null;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    Cancel(snapshot[i]);
                }
                catch (Exception exception)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(exception);
                }
            }

            _runningActions.Clear();
            if (exceptions != null)
            {
                throw new AggregateException("一个或多个 MAction 取消回调执行失败。", exceptions);
            }
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            if (_isShutdown)
            {
                return;
            }

            _isShutdown = true;
            CancelAll();
        }

        /// <summary>关闭该运行器，并取消所有正在运行的动作。</summary>
        public void Dispose()
        {
            Shutdown();
        }

        internal void Cancel(RunningAction runningAction)
        {
            if (runningAction == null || !runningAction.IsRegistered)
            {
                return;
            }

            try
            {
                runningAction.Node.Cancel();
            }
            finally
            {
                Remove(runningAction);
            }
        }

        private static bool IsTerminal(MActionStatus status)
        {
            return status == MActionStatus.Completed
                || status == MActionStatus.Cancelled
                || status == MActionStatus.Failed;
        }

        private void Remove(RunningAction runningAction)
        {
            if (!runningAction.IsRegistered)
            {
                return;
            }

            runningAction.IsRegistered = false;
            runningAction.Owner = null;
            _runningActions.Remove(runningAction);
        }

        internal sealed class RunningAction
        {
            public RunningAction(
                IMActionNode node,
                UnityObject owner,
                bool hasOwner,
                MTimeMode timeMode)
            {
                Node = node;
                Owner = owner;
                HasOwner = hasOwner;
                TimeMode = timeMode;
            }

            public IMActionNode Node { get; }
            public UnityObject Owner { get; set; }
            public bool HasOwner { get; }
            public MTimeMode TimeMode { get; }
            public bool IsRegistered { get; set; }
        }
    }

    /// <summary>
    /// 观察由 <see cref="MActionRunner"/> 启动的一个根动作，并可按需取消它。
    /// </summary>
    public sealed class MActionHandle : IDisposable
    {
        private readonly MActionRunner _runner;
        private readonly MActionRunner.RunningAction _runningAction;

        internal MActionHandle(MActionRunner runner, MActionRunner.RunningAction runningAction)
        {
            _runner = runner;
            _runningAction = runningAction;
        }

        /// <summary>获取动作的当前状态。</summary>
        public MActionStatus Status => _runningAction.Node.Status;

        /// <summary>获取该动作是否已经不再需要帧更新。</summary>
        public bool IsDone => Status == MActionStatus.Completed
            || Status == MActionStatus.Cancelled
            || Status == MActionStatus.Failed;

        /// <summary>获取导致 Failed 状态的异常；如果动作未失败，则返回 null。</summary>
        public Exception Failure => _runningAction.Node.Failure;

        /// <summary>取消该动作。重复调用不会产生额外影响。</summary>
        public void Cancel()
        {
            _runner.Cancel(_runningAction);
        }

        /// <summary>取消该动作。</summary>
        public void Dispose()
        {
            Cancel();
        }
    }
}
