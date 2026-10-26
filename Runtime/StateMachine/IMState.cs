namespace MFramework
{
    /// <summary>
    /// 定义一个显式注册到 <see cref="MStateMachine{TContext}"/> 的状态。
    /// </summary>
    /// <typeparam name="TContext">各状态共用的数据与服务类型。</typeparam>
    public interface IMState<TContext>
    {
        /// <summary>该状态成为当前状态后调用。</summary>
        /// <param name="context">状态机上下文。</param>
        void Enter(TContext context);

        /// <summary>让该状态向前运行一帧。</summary>
        /// <param name="context">状态机上下文。</param>
        /// <param name="deltaTime">自上一帧以来经过的时间，单位为秒。</param>
        void Tick(TContext context, float deltaTime);

        /// <summary>该状态不再作为当前状态前调用。</summary>
        /// <param name="context">状态机上下文。</param>
        void Exit(TContext context);
    }
}
