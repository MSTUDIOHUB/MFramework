using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 使用同一个共享上下文运行显式注册的状态。
    /// </summary>
    /// <typeparam name="TContext">传给每个状态回调的共享数据与服务类型。</typeparam>
    /// <remarks>
    /// 状态按照泛型类型显式注册。状态机只把 <see cref="Type"/> 用作字典键，
    /// 不会扫描程序集、通过反射创建状态，也不会按名称调用成员。
    /// </remarks>
    public sealed class MStateMachine<TContext> : IDisposable
    {
        private readonly Dictionary<Type, IMState<TContext>> _states =
            new Dictionary<Type, IMState<TContext>>();
        private bool _isTransitioning;

        /// <summary>
        /// 创建一个状态机。
        /// </summary>
        /// <param name="context">传给每个状态回调的共享上下文。</param>
        public MStateMachine(TContext context)
        {
            Context = context;
        }

        /// <summary>获取共享的状态上下文。</summary>
        public TContext Context { get; }

        /// <summary>获取当前状态；首次切换前以及调用 <see cref="Stop"/> 后返回 null。</summary>
        public IMState<TContext> CurrentState { get; private set; }

        /// <summary>获取当前状态的具体类型；没有当前状态时返回 null。</summary>
        public Type CurrentStateType => CurrentState?.GetType();

        /// <summary>获取已经显式注册的状态类型数量。</summary>
        public int StateCount => _states.Count;

        /// <summary>
        /// 使用状态的泛型类型注册一个状态实例。
        /// </summary>
        /// <typeparam name="TState">后续切换状态时使用的具体状态类型。</typeparam>
        /// <param name="state">要注册的状态实例。</param>
        /// <returns>当前状态机，可用于链式完成初始化。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="TState"/> 已经注册。</exception>
        public MStateMachine<TContext> Register<TState>(TState state)
            where TState : class, IMState<TContext>
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Type stateType = typeof(TState);
            if (_states.ContainsKey(stateType))
            {
                throw new InvalidOperationException($"状态类型 {stateType.FullName} 已经注册，不能重复注册。");
            }

            _states.Add(stateType, state);
            return this;
        }

        /// <summary>获取指定状态类型是否已经显式注册。</summary>
        /// <typeparam name="TState">要查找的状态类型。</typeparam>
        /// <returns>该状态类型已经注册时返回 <see langword="true"/>。</returns>
        public bool Contains<TState>()
            where TState : class, IMState<TContext>
        {
            return _states.ContainsKey(typeof(TState));
        }

        /// <summary>获取一个必须存在的已注册状态。</summary>
        /// <typeparam name="TState">已注册的状态类型。</typeparam>
        /// <returns>已经注册的状态实例。</returns>
        /// <exception cref="InvalidOperationException">请求的状态类型尚未注册。</exception>
        public TState Get<TState>()
            where TState : class, IMState<TContext>
        {
            IMState<TContext> state;
            if (!_states.TryGetValue(typeof(TState), out state))
            {
                throw new InvalidOperationException($"未找到已注册的状态类型：{typeof(TState).FullName}。");
            }

            return (TState)state;
        }

        /// <summary>
        /// 退出当前状态，并进入一个已注册的状态。
        /// </summary>
        /// <typeparam name="TState">已注册的目标状态类型。</typeparam>
        /// <returns>
        /// 成功切换状态时返回 <see langword="true"/>；请求的状态已经是当前状态时返回
        /// <see langword="false"/>。
        /// </returns>
        /// <remarks>
        /// 不允许在 Enter 或 Exit 中递归调用该方法。请在后续 Tick 中请求下一次切换，
        /// 使生命周期顺序保持清晰、可预测。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 请求的状态尚未注册，或另一次状态切换正在进行。
        /// </exception>
        public bool ChangeState<TState>()
            where TState : class, IMState<TContext>
        {
            if (_isTransitioning)
            {
                throw new InvalidOperationException("不允许在状态的 Enter 或 Exit 回调中递归发起另一次状态切换。");
            }

            TState nextState = Get<TState>();
            if (ReferenceEquals(CurrentState, nextState))
            {
                return false;
            }

            _isTransitioning = true;
            try
            {
                CurrentState?.Exit(Context);
                CurrentState = nextState;

                try
                {
                    CurrentState.Enter(Context);
                }
                catch
                {
                    CurrentState = null;
                    throw;
                }

                return true;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>获取指定的已注册状态当前是否正在运行。</summary>
        /// <typeparam name="TState">要比较的状态类型。</typeparam>
        /// <returns>该已注册状态是当前状态时返回 <see langword="true"/>。</returns>
        public bool IsIn<TState>()
            where TState : class, IMState<TContext>
        {
            IMState<TContext> state;
            return _states.TryGetValue(typeof(TState), out state)
                && ReferenceEquals(CurrentState, state);
        }

        /// <summary>如果存在当前状态，则推进该状态。</summary>
        /// <param name="deltaTime">自上次 Tick 以来经过的时间，单位为秒，不能为负数。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaTime"/> 为负数。</exception>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "状态机帧时间 deltaTime 不能为负数。");
            }

            CurrentState?.Tick(Context, deltaTime);
        }

        /// <summary>退出当前状态，使状态机进入未激活状态。</summary>
        /// <remarks>重复调用不会产生额外影响。</remarks>
        /// <exception cref="InvalidOperationException">另一次状态切换正在进行。</exception>
        public void Stop()
        {
            if (CurrentState == null)
            {
                return;
            }

            if (_isTransitioning)
            {
                throw new InvalidOperationException("不允许在状态的 Enter 或 Exit 回调中调用 Stop()。");
            }

            _isTransitioning = true;
            try
            {
                IMState<TContext> state = CurrentState;
                state.Exit(Context);
                CurrentState = null;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>停止当前状态。</summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
