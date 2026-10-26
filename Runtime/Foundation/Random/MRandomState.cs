using System;

namespace MFramework
{
    /// <summary>保存一个随机流的完整状态及解释该状态所需的算法标识。</summary>
    /// <remarks>
    /// 公开字段用于显式的数据序列化；反序列化后必须交给 <see cref="MRandom.RestoreState"/> 校验。
    /// 默认值不是有效状态。序列化器必须无损保存两个 64 位无符号整数，不能经由浮点数中转。
    /// 本类型不包含游戏概率、已生成结果或命令回执，也不负责持久化提交。
    /// </remarks>
    [Serializable]
    public struct MRandomState
    {
        /// <summary>算法及输出变换的稳定标识；恢复时必须匹配受支持的算法。</summary>
        public string Algorithm;

        /// <summary>状态格式和公开抽样规则的版本；恢复时必须匹配受支持的版本。</summary>
        public int Version;

        /// <summary>下一次生成输出之前的 64 位状态；零也是合法取值。</summary>
        public ulong State;

        /// <summary>PCG 流使用的奇数增量；偶数增量不是本实现接受的有效状态。</summary>
        public ulong Increment;

        /// <summary>构造供传输或持久化使用的状态记录。</summary>
        /// <param name="algorithm">算法稳定标识。</param>
        /// <param name="version">状态格式及抽样规则版本。</param>
        /// <param name="state">生成器内部状态。</param>
        /// <param name="increment">该流的奇数增量。</param>
        /// <remarks>本构造函数只保存字段；合法性检查由生成器恢复入口统一执行。</remarks>
        public MRandomState(string algorithm, int version, ulong state, ulong increment)
        {
            Algorithm = algorithm;
            Version = version;
            State = state;
            Increment = increment;
        }
    }
}
