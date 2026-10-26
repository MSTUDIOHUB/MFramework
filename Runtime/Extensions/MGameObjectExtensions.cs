using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 为常见的 GameObject 组件操作提供小而明确的辅助方法。
    /// </summary>
    public static class MGameObjectExtensions
    {
        /// <summary>
        /// 返回已有组件；GameObject 中不存在该组件时再添加一个。
        /// </summary>
        /// <typeparam name="T">需要获取或添加的 Component 类型。</typeparam>
        /// <param name="gameObject">需要检查并可能修改的 GameObject。</param>
        /// <returns>已有组件或新添加的组件。</returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="gameObject"/> 为 null 或已经销毁时抛出。
        /// </exception>
        public static T GetOrAddComponent<T>(this GameObject gameObject)
            where T : Component
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            if (gameObject.TryGetComponent(out T component))
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }
    }
}
