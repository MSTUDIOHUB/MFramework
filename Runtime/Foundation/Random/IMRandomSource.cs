namespace MFramework
{
    /// <summary>定义可由游戏规则显式注入的非密码学随机来源。</summary>
    /// <remarks>
    /// 调用会推进该来源的内部状态。玩法与表现应持有不同实例；不要依赖全局随机状态。
    /// 接口不要求具体算法，也不承诺任意两个实现产生相同序列。
    /// </remarks>
    public interface IMRandomSource
    {
        /// <summary>取得一个覆盖全部 32 位取值的无符号整数。</summary>
        /// <returns>范围为 [0, 4294967295]，两端均可能取得。</returns>
        uint NextUInt();

        /// <summary>取得从零开始、均匀分布的有界整数。</summary>
        /// <param name="maxExclusive">不包含的上界，必须大于零。</param>
        /// <returns>范围为 [0, <paramref name="maxExclusive"/>) 的整数。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">上界不大于零。</exception>
        int NextInt(int maxExclusive);

        /// <summary>取得指定区间内均匀分布的整数。</summary>
        /// <param name="minInclusive">包含的下界。</param>
        /// <param name="maxExclusive">不包含的上界，必须严格大于下界。</param>
        /// <returns>范围为 [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>) 的整数。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">上界不大于下界。</exception>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>取得大于或等于零、小于一的双精度随机数。</summary>
        /// <returns>范围为 [0, 1)，不会返回 1、NaN 或无穷大。</returns>
        double NextDouble();
    }
}
