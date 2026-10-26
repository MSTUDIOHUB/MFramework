using System;

namespace MFramework
{
    /// <summary>
    /// 标识一个注册到 <see cref="MAudio"/> 的 AudioClip。
    /// </summary>
    /// <remarks>
    /// ID 使用区分大小写的序号字符串比较。玩法代码应把 ID 保存为稳定常量，不要传入场景对象名称。
    /// </remarks>
    public readonly struct MAudioId : IEquatable<MAudioId>
    {
        /// <summary>
        /// 创建一个音频 ID。
        /// </summary>
        /// <param name="value">非空且稳定的标识字符串。</param>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="value"/> 为 null、空字符串或仅包含空白字符时抛出。
        /// </exception>
        public MAudioId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("音频 ID 不能为 null、空字符串或纯空白字符。", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// 获取底层字符串标识。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 获取当前 ID 是否包含可用值。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        /// <inheritdoc />
        public bool Equals(MAudioId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is MAudioId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 使用区分大小写的序号比较方式比较两个音频 ID。
        /// </summary>
        /// <param name="left">第一个 ID。</param>
        /// <param name="right">第二个 ID。</param>
        /// <returns>两个 ID 的值相同时返回 <see langword="true"/>。</returns>
        public static bool operator ==(MAudioId left, MAudioId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 使用区分大小写的序号比较方式比较两个音频 ID。
        /// </summary>
        /// <param name="left">第一个 ID。</param>
        /// <param name="right">第二个 ID。</param>
        /// <returns>两个 ID 的值不同时返回 <see langword="true"/>。</returns>
        public static bool operator !=(MAudioId left, MAudioId right)
        {
            return !left.Equals(right);
        }
    }
}
