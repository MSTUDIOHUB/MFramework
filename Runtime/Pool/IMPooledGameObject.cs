namespace MFramework
{
    /// <summary>
    /// 在 <see cref="UnityEngine.GameObject"/> 进入或离开 <see cref="MPool"/> 时接收生命周期回调。
    /// </summary>
    /// <remarks>
    /// 需要重置玩法状态的组件应实现此接口。回收时应停止仍在运行的 Action、解除事件订阅，
    /// 并清除不能带到下一次生成过程中的状态。
    /// </remarks>
    public interface IMPooledGameObject
    {
        /// <summary>
        /// 对象池实例完成定位并激活后调用。
        /// </summary>
        void OnSpawned();

        /// <summary>
        /// 对象池实例被停用或由对象池销毁前调用。
        /// </summary>
        void OnDespawned();
    }
}
