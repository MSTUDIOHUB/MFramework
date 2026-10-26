using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 向显式注册的观察者发布类型安全的事实事件。
    /// </summary>
    public interface IMEventBus
    {
        /// <summary>
        /// 为指定事件类型订阅一个处理器。
        /// </summary>
        /// <typeparam name="TEvent">事件载荷类型。</typeparam>
        /// <param name="handler">发布事件时同步调用的回调。</param>
        /// <param name="owner">可选的 Unity 所有者。所有者销毁后，该订阅会失效。</param>
        /// <returns>订阅句柄；释放该句柄时会移除订阅。</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler, UnityEngine.Object owner = null);

        /// <summary>
        /// 向当前订阅者快照同步发布一个事件。
        /// </summary>
        /// <typeparam name="TEvent">事件载荷类型。</typeparam>
        /// <param name="message">要发布的事实事件。</param>
        void Publish<TEvent>(TEvent message);

        /// <summary>移除全部订阅。</summary>
        void Clear();
    }
}
