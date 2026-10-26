using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 创建只替换一个分量的 Vector3 副本。
    /// </summary>
    public static class MVectorExtensions
    {
        /// <summary>
        /// 返回替换了 X 分量的新 Vector3。
        /// </summary>
        /// <param name="value">不会被修改的源值。</param>
        /// <param name="x">返回值使用的 X 分量。</param>
        /// <returns>将 <paramref name="value"/> 的 X 分量替换后的副本。</returns>
        public static Vector3 WithX(this Vector3 value, float x)
        {
            return new Vector3(x, value.y, value.z);
        }

        /// <summary>
        /// 返回替换了 Y 分量的新 Vector3。
        /// </summary>
        /// <param name="value">不会被修改的源值。</param>
        /// <param name="y">返回值使用的 Y 分量。</param>
        /// <returns>将 <paramref name="value"/> 的 Y 分量替换后的副本。</returns>
        public static Vector3 WithY(this Vector3 value, float y)
        {
            return new Vector3(value.x, y, value.z);
        }

        /// <summary>
        /// 返回替换了 Z 分量的新 Vector3。
        /// </summary>
        /// <param name="value">不会被修改的源值。</param>
        /// <param name="z">返回值使用的 Z 分量。</param>
        /// <returns>将 <paramref name="value"/> 的 Z 分量替换后的副本。</returns>
        public static Vector3 WithZ(this Vector3 value, float z)
        {
            return new Vector3(value.x, value.y, z);
        }
    }
}
