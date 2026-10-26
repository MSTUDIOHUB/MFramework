using System;

namespace MFramework
{
    /// <summary>
    /// 定义第一版 MFramework Demo 实际使用的少量宿主平台能力。
    /// </summary>
    /// <remarks>
    /// 平台 SDK 类型必须留在适配器程序集内。玩法代码只依赖本接口，因此缺少 MiniHost 包时项目仍能编译。
    /// </remarks>
    public interface IMPlatformBridge
    {
        /// <summary>
        /// 获取当前平台实现用于诊断的简短名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取桥接是否已经可以接收平台调用。
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// 开始初始化平台。
        /// </summary>
        /// <param name="completed">
        /// 初始化结束时调用一次的回调，参数为成功或失败结果。
        /// </param>
        /// <remarks>
        /// 第一版接口不要求桥接提供取消能力。<see cref="MPlatformService"/> 关闭后会清理等待者，
        /// 并忽略桥接随后触发的迟到回调。
        /// </remarks>
        void Initialize(Action<MPlatformInitializationResult> completed);

        /// <summary>
        /// 请求一次短震动；不支持震动的实现可以安全地不执行任何操作。
        /// </summary>
        void VibrateShort();

        /// <summary>
        /// 从平台存储读取字符串。
        /// </summary>
        /// <param name="key">非空的存储 Key。</param>
        /// <param name="fallback">尚未保存该 Key 时返回的默认值。</param>
        /// <returns>已保存的值；不存在时返回 <paramref name="fallback"/>。</returns>
        string LoadString(string key, string fallback);

        /// <summary>
        /// 向平台存储写入字符串。
        /// </summary>
        /// <param name="key">非空的存储 Key。</param>
        /// <param name="value">需要保存的非 null 字符串。</param>
        void SaveString(string key, string value);
    }
}
