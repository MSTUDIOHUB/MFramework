namespace MFramework
{
    /// <summary>
    /// 定义框架模块的初始化与关闭回调。
    /// </summary>
    public interface IMFrameworkModule
    {
        /// <summary>
        /// 所有服务注册完成后，初始化该模块。
        /// </summary>
        /// <param name="app">持有该模块的框架应用。</param>
        void Initialize(MFrameworkApp app);

        /// <summary>
        /// 释放该模块持有的资源；即使尚未初始化或初始化中途失败，也必须能够安全调用。
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 定义每帧更新回调。
    /// </summary>
    public interface IMFrameworkTickable
    {
        /// <summary>
        /// 让对象向前运行一帧。
        /// </summary>
        /// <param name="time">缩放与非缩放的帧时间。</param>
        void Tick(MFrameTime time);
    }

    /// <summary>
    /// 定义固定时间步长的更新回调。
    /// </summary>
    public interface IMFrameworkFixedTickable
    {
        /// <summary>
        /// 让对象向前运行一个固定时间步长。
        /// </summary>
        /// <param name="fixedDeltaTime">固定时间步长，单位为秒。</param>
        void FixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// 定义帧末更新回调。
    /// </summary>
    public interface IMFrameworkLateTickable
    {
        /// <summary>
        /// 在常规帧更新完成后推进对象。
        /// </summary>
        /// <param name="time">缩放与非缩放的帧时间。</param>
        void LateTick(MFrameTime time);
    }

    /// <summary>
    /// 描述 <see cref="MFrameworkApp"/> 的生命周期状态。
    /// </summary>
    public enum MFrameworkAppState
    {
        /// <summary>应用正在接受服务注册。</summary>
        Created,
        /// <summary>应用正在初始化模块。</summary>
        Initializing,
        /// <summary>应用已经初始化，可以接收 Tick。</summary>
        Running,
        /// <summary>应用正在关闭模块。</summary>
        ShuttingDown,
        /// <summary>应用已经关闭。</summary>
        Shutdown,
        /// <summary>应用初始化失败。</summary>
        Faulted
    }
}
