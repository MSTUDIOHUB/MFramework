using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>保存用于创建 <see cref="MUIService"/> 的三个 UGUI 层级 Transform。</summary>
    [DisallowMultipleComponent]
    public sealed class MUIRoot : MonoBehaviour
    {
        [SerializeField]
        private RectTransform pageLayer;

        [SerializeField]
        private RectTransform popupLayer;

        [SerializeField]
        private RectTransform overlayLayer;

        /// <summary>获取页面层。</summary>
        public RectTransform PageLayer => pageLayer;

        /// <summary>获取模态弹窗层。</summary>
        public RectTransform PopupLayer => popupLayer;

        /// <summary>获取非模态覆盖层。</summary>
        public RectTransform OverlayLayer => overlayLayer;

        /// <summary>校验全部必需的层级引用后创建 UI 服务。</summary>
        /// <returns>由调用方或 <see cref="MFrameworkApp"/> 持有的新 UI 服务。</returns>
        public MUIService CreateService()
        {
            if (pageLayer == null || popupLayer == null || overlayLayer == null)
            {
                throw new InvalidOperationException(
                    "MUIRoot 必须完整配置 Page Layer、Popup Layer 与 Overlay Layer 三个层级引用。");
            }

            if (pageLayer == popupLayer || pageLayer == overlayLayer || popupLayer == overlayLayer)
            {
                throw new InvalidOperationException("MUIRoot 的 Page、Popup 与 Overlay 必须是三个不同层级。");
            }

            if (pageLayer.parent != popupLayer.parent || pageLayer.parent != overlayLayer.parent)
            {
                throw new InvalidOperationException("MUIRoot 的三个层级必须拥有同一个父节点，才能保证稳定绘制顺序。");
            }

            // 同一 Canvas 下，较后的兄弟节点绘制在较上方。
            pageLayer.SetAsLastSibling();
            popupLayer.SetAsLastSibling();
            overlayLayer.SetAsLastSibling();

            return new MUIService(pageLayer, popupLayer, overlayLayer);
        }
    }
}
