using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 提供可在编辑器安全运行的平台降级实现：使用 PlayerPrefs 存储，震动请求不执行操作。
    /// </summary>
    /// <remarks>
    /// 此桥接特意不引用任何 MiniHost SDK 类型，因此也可用于不受支持的开发构建，保证核心玩法降级运行。
    /// </remarks>
    public sealed class MEditorPlatformBridge : IMPlatformBridge
    {
        /// <inheritdoc />
        public string Name => "Editor";

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public void Initialize(Action<MPlatformInitializationResult> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            IsReady = true;
            completed(MPlatformInitializationResult.Success());
        }

        /// <inheritdoc />
        public void VibrateShort()
        {
            // 编辑器没有对应的震动行为；静默忽略可以让降级实现继续工作。
        }

        /// <inheritdoc />
        public string LoadString(string key, string fallback)
        {
            ValidateKey(key);

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            return PlayerPrefs.GetString(key, fallback);
        }

        /// <inheritdoc />
        public void SaveString(string key, string value)
        {
            ValidateKey(key);

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("平台存储 Key 不能为 null、空字符串或纯空白字符。", nameof(key));
            }
        }
    }
}
