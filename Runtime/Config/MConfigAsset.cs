using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 为 ScriptableObject 配置项提供稳定标识和校验入口。
    /// </summary>
    public abstract class MConfigAsset : ScriptableObject
    {
        [SerializeField]
        private string id;

        /// <summary>获取供 <see cref="MConfig"/> 使用的稳定标识。</summary>
        public string Id => id;

        /// <summary>
        /// 将该配置项专用的校验错误加入给定上下文。
        /// </summary>
        /// <param name="context">该配置项的校验上下文。</param>
        /// <remarks>
        /// 基类实现不会添加任何错误。重写时可使用上下文的 AddError 报告业务校验问题，
        /// 或 RequireReference 显式检查其他已注册配置。请通过 MConfig.ValidateAll 在全部配置
        /// 注册完成后统一调用；不得在校验中修改注册表、改写资产或递归校验引用目标。
        /// </remarks>
        public virtual void Validate(MConfigValidationContext context)
        {
        }
    }
}
