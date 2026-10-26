using System;

namespace MFramework
{
    /// <summary>所有 MFramework Addressables 请求共同提供的只读状态接口。</summary>
    public interface IMAddressablesRequest : IDisposable
    {
        /// <summary>获取请求当前所处的状态。</summary>
        MAddressablesRequestState State { get; }

        /// <summary>获取从 0 到 1 的近似完成进度。</summary>
        float Progress { get; }

        /// <summary>请求进入 <see cref="MAddressablesRequestState.Failed"/> 后，获取失败详情。</summary>
        MAddressablesError Error { get; }

        /// <summary>获取调用方是否已经不必再等待此请求。</summary>
        bool IsDone { get; }
    }

    /// <summary>描述 MFramework Addressables 请求的当前状态。</summary>
    public enum MAddressablesRequestState
    {
        /// <summary>底层 Addressables 操作尚未完成。</summary>
        Pending,

        /// <summary>操作已经成功完成。</summary>
        Succeeded,

        /// <summary>操作因错误而结束。</summary>
        Failed,

        /// <summary>调用方在请求完成前停止了观察。</summary>
        Abandoned
    }

    /// <summary>保存便于阅读的 Addressables 失败信息及其原始异常。</summary>
    public sealed class MAddressablesError
    {
        /// <summary>创建一条 Addressables 错误信息。</summary>
        /// <param name="key">本次操作使用的地址、标签或运行时键。</param>
        /// <param name="message">适合日志记录和问题诊断的消息。</param>
        /// <param name="exception">Addressables 提供的原始异常；没有时为 null。</param>
        public MAddressablesError(object key, string message, Exception exception = null)
        {
            Key = key;
            Message = string.IsNullOrEmpty(message) ? "Addressables 操作失败。" : message;
            Exception = exception;
        }

        /// <summary>获取发生失败的地址、标签或运行时键。</summary>
        public object Key { get; }

        /// <summary>获取便于阅读的失败说明。</summary>
        public string Message { get; }

        /// <summary>获取底层报告的原始异常；没有时为 null。</summary>
        public Exception Exception { get; }

        /// <summary>返回简洁的诊断字符串。</summary>
        public override string ToString()
        {
            return "Addressables 键 '" + (Key ?? "<null>") + "': " + Message;
        }
    }
}
