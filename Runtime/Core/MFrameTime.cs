namespace MFramework
{
    /// <summary>
    /// 保存自上一帧以来经过的缩放时间与非缩放时间。
    /// </summary>
    public readonly struct MFrameTime
    {
        /// <summary>
        /// 创建一个帧时间值。
        /// </summary>
        /// <param name="deltaTime">缩放后的帧间隔，单位为秒。</param>
        /// <param name="unscaledDeltaTime">未缩放的帧间隔，单位为秒。</param>
        public MFrameTime(float deltaTime, float unscaledDeltaTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }

        /// <summary>
        /// 获取缩放后的帧间隔，单位为秒。
        /// </summary>
        public float DeltaTime { get; }

        /// <summary>
        /// 获取未缩放的帧间隔，单位为秒。
        /// </summary>
        public float UnscaledDeltaTime { get; }
    }
}
