using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 定义 GameObject 对象池的显式注册与生命周期操作。
    /// </summary>
    public interface IMPool
    {
        /// <summary>
        /// 获取对象池当前持有的预制体注册数量。
        /// </summary>
        int RegisteredCount { get; }

        /// <summary>
        /// 注册预制体。预制体必须先注册，之后才能预热或生成。
        /// </summary>
        /// <param name="key">后续操作使用的稳定 Key。</param>
        /// <param name="prefab">需要实例化的 GameObject 预制体。</param>
        /// <param name="maxInactive">
        /// 最多保留的闲置实例数量；零表示不限制闲置数量。
        /// </param>
        void Register(MPoolKey key, GameObject prefab, int maxInactive = 0);

        /// <summary>
        /// 获取指定 Key 是否已经注册预制体。
        /// </summary>
        /// <param name="key">需要查询的 Key。</param>
        /// <returns>Key 已注册时返回 <see langword="true"/>。</returns>
        bool IsRegistered(MPoolKey key);

        /// <summary>
        /// 确保至少准备好 <paramref name="count"/> 个闲置实例。
        /// </summary>
        /// <param name="key">已注册预制体的 Key。</param>
        /// <param name="count">目标闲置实例数量。</param>
        void Prewarm(MPoolKey key, int count);

        /// <summary>
        /// 获取并激活一个已注册预制体的实例。
        /// </summary>
        /// <param name="key">已注册预制体的 Key。</param>
        /// <param name="position">生命周期回调执行前设置的世界坐标。</param>
        /// <param name="rotation">生命周期回调执行前设置的世界旋转。</param>
        /// <param name="parent">为实例设置的可选父级。</param>
        /// <returns>已激活的对象池 GameObject。</returns>
        GameObject Spawn(MPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null);

        /// <summary>
        /// 将活跃实例归还给它所属的对象池。
        /// </summary>
        /// <param name="instance">之前由 <see cref="Spawn"/> 返回的实例。</param>
        void Despawn(GameObject instance);

        /// <summary>
        /// 销毁指定 Key 的全部实例，并移除对应的预制体注册。
        /// </summary>
        /// <param name="key">需要清理的已注册 Key。</param>
        void Clear(MPoolKey key);

        /// <summary>
        /// 销毁全部受管理实例，并移除所有预制体注册。
        /// </summary>
        void ClearAll();
    }
}
