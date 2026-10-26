using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 报告发布事件时一个或多个订阅者发生的异常。
    /// </summary>
    public sealed class MEventDispatchException : AggregateException
    {
        /// <summary>
        /// 创建该异常。
        /// </summary>
        /// <param name="eventType">正在发布的事件载荷类型。</param>
        /// <param name="innerExceptions">订阅者抛出的异常。</param>
        public MEventDispatchException(Type eventType, IEnumerable<Exception> innerExceptions)
            : base($"发布事件 {eventType.FullName} 时有一个或多个订阅者执行失败。", innerExceptions)
        {
            EventType = eventType;
        }

        /// <summary>获取异常发生时正在发布的事件载荷类型。</summary>
        public Type EventType { get; }
    }
}
