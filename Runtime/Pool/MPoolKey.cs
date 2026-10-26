using System;

namespace MFramework
{
    /// <summary>
    /// 标识一个在 <see cref="MPool"/> 中显式注册的预制体。
    /// </summary>
    /// <remarks>
    /// Key 使用区分大小写的序号字符串比较。建议使用稳定常量，不要根据场景对象的显示名称临时构造 Key。
    /// </remarks>
    public readonly struct MPoolKey : IEquatable<MPoolKey>
    {
        /// <summary>
        /// 创建一个对象池 Key。
        /// </summary>
        /// <param name="value">非空且稳定的标识字符串。</param>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="value"/> 为 null、空字符串或仅包含空白字符时抛出。
        /// </exception>
        public MPoolKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("对象池 Key 不能为 null、空字符串或纯空白字符。", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// 获取用于标识已注册预制体的字符串值。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 获取当前 Key 是否包含可用值。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        /// <inheritdoc />
        public bool Equals(MPoolKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is MPoolKey other && Equals(other);
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
        /// 使用区分大小写的序号比较方式比较两个对象池 Key。
        /// </summary>
        /// <param name="left">第一个 Key。</param>
        /// <param name="right">第二个 Key。</param>
        /// <returns>两个 Key 的值相同时返回 <see langword="true"/>。</returns>
        public static bool operator ==(MPoolKey left, MPoolKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 使用区分大小写的序号比较方式比较两个对象池 Key。
        /// </summary>
        /// <param name="left">第一个 Key。</param>
        /// <param name="right">第二个 Key。</param>
        /// <returns>两个 Key 的值不同时返回 <see langword="true"/>。</returns>
        public static bool operator !=(MPoolKey left, MPoolKey right)
        {
            return !left.Equals(right);
        }
    }
}
