using System;

namespace MFramework
{
    /// <summary>
    /// 描述平台桥接初始化是否成功完成。
    /// </summary>
    public readonly struct MPlatformInitializationResult
    {
        /// <summary>
        /// 创建一个平台初始化结果。
        /// </summary>
        /// <param name="succeeded">初始化是否成功。</param>
        /// <param name="errorMessage">
        /// 可用于定位问题的失败说明；成功结果始终保存空字符串。
        /// </param>
        /// <exception cref="ArgumentException">
        /// 当失败结果未提供错误说明时抛出。
        /// </exception>
        public MPlatformInitializationResult(bool succeeded, string errorMessage)
        {
            if (!succeeded && string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException(
                    "失败的平台初始化结果必须提供非空的错误说明。",
                    nameof(errorMessage));
            }

            Succeeded = succeeded;
            ErrorMessage = succeeded ? string.Empty : errorMessage;
        }

        /// <summary>
        /// 获取平台初始化是否成功。
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 获取失败说明；初始化成功时返回空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 创建一个成功的初始化结果。
        /// </summary>
        /// <returns><see cref="Succeeded"/> 为 <see langword="true"/> 的结果。</returns>
        public static MPlatformInitializationResult Success()
        {
            return new MPlatformInitializationResult(true, string.Empty);
        }

        /// <summary>
        /// 创建一个失败的初始化结果。
        /// </summary>
        /// <param name="errorMessage">适合写入日志或降级 UI 的非空说明。</param>
        /// <returns><see cref="Succeeded"/> 为 <see langword="false"/> 的结果。</returns>
        public static MPlatformInitializationResult Failure(string errorMessage)
        {
            return new MPlatformInitializationResult(false, errorMessage);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Succeeded ? "Succeeded" : $"Failed: {ErrorMessage}";
        }
    }
}
