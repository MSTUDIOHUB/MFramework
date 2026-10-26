// PCG algorithm based on the PCG C minimal implementation by Melissa O'Neill.
// Ported to C# and adapted by MSTUDIO, 2026. See Third Party Notices.md.
using System;

namespace MFramework
{
    /// <summary>使用固定 PCG-XSH-RR 64/32 算法提供可恢复的独立随机流。</summary>
    /// <remarks>
    /// 同一初始状态、同一调用顺序和同一版本产生相同结果；不同抽样方法消耗的输出数量可能不同。
    /// 本类型不是密码学随机数生成器，不提供防作弊、线程安全或持久化保证。
    /// 调用方负责串行访问，并把捕获状态与对应业务结果一同提交。
    /// </remarks>
    public sealed class MRandom : IMRandomSource
    {
        /// <summary>此实现接受的算法标识。</summary>
        public const string AlgorithmId = "pcg-xsh-rr-64-32";

        /// <summary>状态布局、播种、区间抽样、双精度抽样及命名流派生规则的当前版本。</summary>
        public const int StateVersion = 1;

        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private ulong _increment;

        /// <summary>按照 PCG 标准播种过程创建一个随机流。</summary>
        /// <param name="seed">初始种子，接受所有 64 位无符号整数。</param>
        /// <param name="sequence">流编号，范围为 [0, 2^63 - 1]；默认值为 54。</param>
        /// <exception cref="ArgumentOutOfRangeException">流编号超出 63 位可表示范围。</exception>
        /// <remarks>显式限制流编号，避免左移时丢弃最高位，导致两个输入悄悄映射到同一增量。</remarks>
        public MRandom(ulong seed, ulong sequence = 54UL)
        {
            if (sequence > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "随机流编号不能超过 2^63 - 1。");
            }

            _increment = (sequence << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        /// <summary>从先前捕获的有效状态恢复一个随机流，不额外消耗输出。</summary>
        /// <param name="state">包含算法、版本、内部状态及增量的完整记录。</param>
        /// <exception cref="ArgumentException">算法或版本不受支持，或增量不是奇数。</exception>
        public MRandom(MRandomState state)
        {
            RestoreState(state);
        }

        /// <summary>从主种子和稳定名称派生一个互不共享可变状态的随机流。</summary>
        /// <param name="seed">由应用保存的主种子。</param>
        /// <param name="streamName">非空且非全空白的稳定名称，区分大小写，不进行 Unicode 规范化。</param>
        /// <returns>新生成器；相同种子、名称及版本得到相同序列。</returns>
        /// <exception cref="ArgumentException">名称为 null、空字符串或全部为空白。</exception>
        /// <remarks>
        /// 派生不读取也不推进任何已有生成器。名称按 UTF-16 码元的小端字节使用 FNV-1a 64 位散列，
        /// 不使用运行时的 string.GetHashCode。散列不保证不同名称永不碰撞，也不保证流之间密码学独立。
        /// </remarks>
        public static MRandom CreateStream(ulong seed, string streamName)
        {
            if (string.IsNullOrWhiteSpace(streamName))
            {
                throw new ArgumentException("随机流名称不能为空或全部为空白。", nameof(streamName));
            }

            // 明确逐字节的顺序，使平台字节序、字符串散列随机化和文本编码设置都不影响结果。
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < streamName.Length; i++)
            {
                uint codeUnit = streamName[i];
                hash = unchecked((hash ^ (codeUnit & 0xffU)) * 1099511628211UL);
                hash = unchecked((hash ^ (codeUnit >> 8)) * 1099511628211UL);
            }

            return new MRandom(seed ^ hash, hash >> 1);
        }

        /// <summary>捕获当前流的完整状态，不推进生成器。</summary>
        /// <returns>可以由应用与业务快照共同序列化的独立值副本。</returns>
        public MRandomState CaptureState()
        {
            return new MRandomState(AlgorithmId, StateVersion, _state, _increment);
        }

        /// <summary>校验并恢复状态；校验失败不会改变当前流。</summary>
        /// <param name="state">先前捕获或无损反序列化得到的状态。</param>
        /// <exception cref="ArgumentException">算法或版本不受支持，或增量不是奇数。</exception>
        /// <remarks>此方法不会自动迁移旧版本，也不会在无效状态时重新播种，以免静默改变游戏结果。</remarks>
        public void RestoreState(MRandomState state)
        {
            if (!string.Equals(state.Algorithm, AlgorithmId, StringComparison.Ordinal))
            {
                throw new ArgumentException("随机状态的算法不受支持。", nameof(state));
            }

            if (state.Version != StateVersion)
            {
                throw new ArgumentException("随机状态的版本不受支持。", nameof(state));
            }

            if ((state.Increment & 1UL) == 0)
            {
                throw new ArgumentException("随机状态的增量必须为奇数。", nameof(state));
            }

            _state = state.State;
            _increment = state.Increment;
        }

        /// <inheritdoc />
        public uint NextUInt()
        {
            ulong previous = _state;
            // PCG 的线性同余状态按模 2^64 运算；即使调用项目启用溢出检查也保持固定序列。
            _state = unchecked(previous * Multiplier + _increment);
            uint shifted = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
            int rotation = (int)(previous >> 59);
            return (shifted >> rotation) | (shifted << ((-rotation) & 31));
        }

        /// <inheritdoc />
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "随机整数上界必须大于零。");
            }

            return (int)NextBounded((uint)maxExclusive);
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "随机整数上界必须严格大于下界。");
            }

            // 在 64 位中计算宽度，支持跨越零且宽度超过 int.MaxValue 的区间。
            uint width = (uint)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + NextBounded(width));
        }

        /// <inheritdoc />
        /// <remarks>固定消耗两个 32 位输出，组合为 53 位有效数，再除以 2^53。</remarks>
        public double NextDouble()
        {
            uint high = NextUInt() >> 5;
            uint low = NextUInt() >> 6;
            return (high * 67108864.0 + low) / 9007199254740992.0;
        }

        private uint NextBounded(uint bound)
        {
            // 拒绝 2^32 除以区间宽度后的余数部分，消除直接取模引入的偏差。
            uint threshold = unchecked(0U - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return value % bound;
                }
            }
        }
    }
}
