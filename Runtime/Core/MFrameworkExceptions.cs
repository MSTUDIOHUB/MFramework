using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 报告模块初始化失败，以及回滚过程中发生的其他失败。
    /// </summary>
    public sealed class MFrameworkInitializationException : Exception
    {
        /// <summary>
        /// 创建该异常。
        /// </summary>
        /// <param name="moduleType">初始化失败的模块类型。</param>
        /// <param name="innerException">最初发生的初始化异常。</param>
        /// <param name="rollbackExceptions">关闭已经初始化的模块时发生的异常。</param>
        public MFrameworkInitializationException(
            Type moduleType,
            Exception innerException,
            IReadOnlyList<Exception> rollbackExceptions)
            : base($"初始化框架模块 {moduleType.FullName} 失败。", innerException)
        {
            ModuleType = moduleType;
            RollbackExceptions = rollbackExceptions ?? Array.Empty<Exception>();
        }

        /// <summary>获取初始化失败的模块类型。</summary>
        public Type ModuleType { get; }

        /// <summary>获取回滚先前模块时发生的异常。</summary>
        public IReadOnlyList<Exception> RollbackExceptions { get; }
    }

    /// <summary>
    /// 报告一个或多个模块关闭失败。
    /// </summary>
    public sealed class MFrameworkShutdownException : AggregateException
    {
        /// <summary>
        /// 创建该异常。
        /// </summary>
        /// <param name="innerExceptions">关闭模块时发生的异常。</param>
        public MFrameworkShutdownException(IEnumerable<Exception> innerExceptions)
            : base("一个或多个框架模块在关闭时发生异常。", innerExceptions)
        {
        }
    }
}
