using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 提供只修改 RectTransform 单个 anchoredPosition 轴的辅助方法。
    /// </summary>
    public static class MRectTransformExtensions
    {
        /// <summary>
        /// 替换 anchoredPosition 的 X 值，同时保留当前 Y 值。
        /// </summary>
        /// <param name="rectTransform">需要修改的 RectTransform。</param>
        /// <param name="x">新的 anchoredPosition X 值。</param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="rectTransform"/> 为 null 或已经销毁时抛出。
        /// </exception>
        public static void SetAnchoredPositionX(this RectTransform rectTransform, float x)
        {
            if (rectTransform == null)
            {
                throw new ArgumentNullException(nameof(rectTransform));
            }

            Vector2 position = rectTransform.anchoredPosition;
            position.x = x;
            rectTransform.anchoredPosition = position;
        }

        /// <summary>
        /// 替换 anchoredPosition 的 Y 值，同时保留当前 X 值。
        /// </summary>
        /// <param name="rectTransform">需要修改的 RectTransform。</param>
        /// <param name="y">新的 anchoredPosition Y 值。</param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="rectTransform"/> 为 null 或已经销毁时抛出。
        /// </exception>
        public static void SetAnchoredPositionY(this RectTransform rectTransform, float y)
        {
            if (rectTransform == null)
            {
                throw new ArgumentNullException(nameof(rectTransform));
            }

            Vector2 position = rectTransform.anchoredPosition;
            position.y = y;
            rectTransform.anchoredPosition = position;
        }
    }
}
