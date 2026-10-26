using System;

namespace MFramework
{
    /// <summary>
    /// 描述 <see cref="IMActionNode"/> 的生命周期状态。
    /// </summary>
    public enum MActionStatus
    {
        /// <summary>动作尚未启动。</summary>
        Created,
        /// <summary>动作正在接收每帧更新。</summary>
        Running,
        /// <summary>动作已自然完成。</summary>
        Completed,
        /// <summary>动作在自然完成前被取消。</summary>
        Cancelled,
        /// <summary>动作因某个回调抛出异常而停止。</summary>
        Failed
    }

    /// <summary>
    /// 选择使用哪一种 Unity 帧时间推进动作。
    /// </summary>
    public enum MTimeMode
    {
        /// <summary>使用缩放时间，因此会受到 <c>Time.timeScale</c> 影响。</summary>
        Scaled,
        /// <summary>使用非缩放时间，因此游戏暂停时仍可继续运行。</summary>
        Unscaled
    }

    /// <summary>
    /// 表示自定义动作是否还需要继续运行一帧。
    /// </summary>
    public enum MActionTickResult
    {
        /// <summary>自定义动作仍在运行。</summary>
        Running,
        /// <summary>自定义动作已经完成。</summary>
        Completed
    }

    /// <summary>
    /// 定义一个可由 <see cref="MActionRunner"/> 确定性驱动的动作节点。
    /// </summary>
    /// <remarks>
    /// 每个节点只能使用一次。每次启动流程时都应创建新节点；需要重复动作时，请使用工厂创建节点。
    /// </remarks>
    public interface IMActionNode
    {
        /// <summary>获取当前生命周期状态。</summary>
        MActionStatus Status { get; }

        /// <summary>获取导致 Failed 状态的异常；如果动作未失败，则返回 null。</summary>
        Exception Failure { get; }

        /// <summary>启动该节点。每个节点只能启动一次。</summary>
        /// <exception cref="InvalidOperationException">该节点已经启动过。</exception>
        void Start();

        /// <summary>推进一个正在运行的节点。</summary>
        /// <param name="deltaTime">自上次 Tick 以来经过的时间，单位为秒，不能为负数。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaTime"/> 为负数。</exception>
        /// <exception cref="InvalidOperationException">该节点尚未启动。</exception>
        void Tick(float deltaTime);

        /// <summary>取消该节点及其所有正在运行的子节点。</summary>
        void Cancel();
    }

    /// <summary>
    /// 提供动作节点共用的生命周期实现。
    /// </summary>
    public abstract class MActionNode : IMActionNode
    {
        /// <summary>创建一个初始状态为 Created 的动作节点。</summary>
        protected MActionNode()
        {
        }

        /// <inheritdoc/>
        public MActionStatus Status { get; private set; } = MActionStatus.Created;

        /// <inheritdoc/>
        public Exception Failure { get; private set; }

        /// <inheritdoc/>
        public void Start()
        {
            if (Status != MActionStatus.Created)
            {
                throw new InvalidOperationException("动作节点只能启动一次，请勿复用同一个节点实例。");
            }

            Status = MActionStatus.Running;
            try
            {
                OnStart();
            }
            catch (Exception exception)
            {
                MarkFailed(exception);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "动作帧时间 deltaTime 不能为负数。");
            }

            if (Status == MActionStatus.Created)
            {
                throw new InvalidOperationException("必须先调用 Start() 启动动作节点，然后才能调用 Tick()。");
            }

            if (Status != MActionStatus.Running)
            {
                return;
            }

            try
            {
                OnTick(deltaTime);
            }
            catch (Exception exception)
            {
                MarkFailed(exception);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            if (IsTerminal(Status))
            {
                return;
            }

            try
            {
                OnCancel();
            }
            finally
            {
                Status = MActionStatus.Cancelled;
                OnTerminated();
            }
        }

        /// <summary>该节点启动时调用。</summary>
        protected virtual void OnStart()
        {
        }

        /// <summary>让该节点向前运行一帧。</summary>
        /// <param name="deltaTime">自上一帧以来经过的时间，单位为秒。</param>
        protected abstract void OnTick(float deltaTime);

        /// <summary>该节点被取消时调用。</summary>
        protected virtual void OnCancel()
        {
        }

        /// <summary>该节点进入任意终止状态后调用。</summary>
        protected virtual void OnTerminated()
        {
        }

        /// <summary>将该运行中的节点标记为自然完成。</summary>
        /// <exception cref="InvalidOperationException">该节点当前不在运行。</exception>
        protected void Complete()
        {
            if (Status != MActionStatus.Running)
            {
                throw new InvalidOperationException("只有处于 Running 状态的动作节点才能标记为完成。");
            }

            Status = MActionStatus.Completed;
            OnTerminated();
        }

        private static bool IsTerminal(MActionStatus status)
        {
            return status == MActionStatus.Completed
                || status == MActionStatus.Cancelled
                || status == MActionStatus.Failed;
        }

        private void MarkFailed(Exception exception)
        {
            Failure = exception;
            Status = MActionStatus.Failed;
            OnTerminated();
        }
    }
}
