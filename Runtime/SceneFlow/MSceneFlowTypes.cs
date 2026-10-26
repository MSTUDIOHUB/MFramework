using System;
using UnityEngine.SceneManagement;

namespace MFramework
{
    /// <summary>选择一次内容场景切换中加载目标与卸载旧场景的先后顺序。</summary>
    public enum MSceneFlowOrder
    {
        /// <summary>
        /// 先加载、激活并验证目标，再卸载旧场景。失败时最有机会保留旧场景，但内存峰值较高。
        /// </summary>
        LoadThenUnload,

        /// <summary>
        /// 先卸载旧场景，再加载目标。内存峰值较低，但卸载开始后无法零成本恢复旧场景。
        /// </summary>
        UnloadThenLoad
    }

    /// <summary>描述 MSceneFlow 当前所在的确定性执行阶段。</summary>
    public enum MSceneFlowStage
    {
        /// <summary>尚未开始。</summary>
        None,
        /// <summary>整次切换开始前的命令点。</summary>
        BeforeTransition,
        /// <summary>发起目标场景加载前的命令点。</summary>
        BeforeLoad,
        /// <summary>正在加载尚未激活的目标场景。</summary>
        Loading,
        /// <summary>目标场景加载完成、开始激活前的命令点。</summary>
        AfterLoad,
        /// <summary>开始激活目标场景前的命令点。</summary>
        BeforeActivation,
        /// <summary>正在激活目标场景。</summary>
        Activating,
        /// <summary>目标场景激活完成后的命令点。</summary>
        AfterActivation,
        /// <summary>把目标设为 Unity Active Scene 前的命令点。</summary>
        BeforeSetActiveScene,
        /// <summary>正在把目标设为 Unity Active Scene。</summary>
        SettingActiveScene,
        /// <summary>目标已成为 Unity Active Scene 后的命令点。</summary>
        AfterSetActiveScene,
        /// <summary>发起旧场景卸载前的命令点。</summary>
        BeforeUnload,
        /// <summary>正在卸载旧场景。</summary>
        Unloading,
        /// <summary>旧场景物理卸载完成后的命令点。</summary>
        AfterUnload,
        /// <summary>所有成功路径步骤完成后的命令点。</summary>
        AfterTransition,
        /// <summary>失败或取消后，正在回收尚未提交的目标场景。</summary>
        CleaningTarget,
        /// <summary>成功、失败或取消路径都会执行的最终命令点。</summary>
        Finally,
        /// <summary>切换已成功完成。</summary>
        Completed,
        /// <summary>切换因错误结束。</summary>
        Failed,
        /// <summary>切换因调用方取消结束。</summary>
        Cancelled
    }

    /// <summary>描述一个 MSceneFlow 请求的生命周期状态。</summary>
    public enum MSceneFlowState
    {
        /// <summary>请求正在执行阶段或用户命令。</summary>
        Running,
        /// <summary>已经请求取消，正在等待不可中断的 Unity 操作安全收口。</summary>
        Cancelling,
        /// <summary>全部阶段和最终命令已完成。</summary>
        Succeeded,
        /// <summary>某个阶段、命令或最终清理失败。</summary>
        Failed,
        /// <summary>请求已经安全取消。</summary>
        Cancelled
    }

    /// <summary>表示由场景后端创建、并由 MSceneFlow 持有的内容场景。</summary>
    public interface IMSceneFlowScene : IDisposable
    {
        /// <summary>获取对应的 Unity 场景。</summary>
        Scene Scene { get; }

        /// <summary>获取场景所有权是否已经交还给后端。</summary>
        bool IsReleased { get; }
    }

    /// <summary>提供 MSceneFlow 可轮询的异步操作最小契约。</summary>
    public interface IMSceneFlowOperation : IDisposable
    {
        /// <summary>获取底层操作是否已经结束。</summary>
        bool IsDone { get; }

        /// <summary>获取从 0 到 1 的近似进度。</summary>
        float Progress { get; }

        /// <summary>获取操作失败的原始异常；成功或仍在运行时为 null。</summary>
        Exception Failure { get; }

        /// <summary>
        /// 停止观察并释放包装器资源；不保证能够取消已经提交给 Unity 的底层操作。
        /// </summary>
        new void Dispose();
    }

    /// <summary>表示一次尚未转移目标场景所有权的加载操作。</summary>
    /// <remarks>
    /// 在调用 <see cref="TakeScene"/> 前释放本操作，表示调用方永久放弃加载结果。即使底层加载
    /// 稍后成功，后端也必须继续持有并最终释放该场景，不能把所有权留给已经结束的 MSceneFlow。
    /// </remarks>
    public interface IMSceneFlowLoadOperation : IMSceneFlowOperation
    {
        /// <summary>
        /// 在成功完成后把场景所有权转移给 MSceneFlow；只能调用一次。
        /// </summary>
        /// <returns>由调用方负责最终释放的场景对象。</returns>
        IMSceneFlowScene TakeScene();
    }

    /// <summary>
    /// 隔离 MSceneFlow 与具体场景提供方，并让加载、激活和卸载顺序可以独立测试。
    /// </summary>
    public interface IMSceneFlowBackend
    {
        /// <summary>
        /// 以 Additive 且暂不激活的方式开始加载目标内容场景。
        /// </summary>
        /// <remarks>
        /// 返回的操作必须遵守 <see cref="IMSceneFlowLoadOperation"/> 的放弃契约：在场景所有权
        /// 转移前被释放时，后端负责处理并最终释放任何迟到的成功结果。
        /// </remarks>
        IMSceneFlowLoadOperation LoadAsync(object key, int priority);

        /// <summary>开始激活一个已经加载但尚未激活的目标场景。</summary>
        IMSceneFlowOperation ActivateAsync(IMSceneFlowScene scene);

        /// <summary>
        /// 开始卸载场景。操作运行期间调用方不得释放场景；成功后后端完成所有权交还，
        /// 失败时场景仍归调用方持有，可再次释放或重试。
        /// </summary>
        /// <remarks>
        /// 若运行中的操作被 Dispose，后端仍必须负责让已经发起的底层卸载安全收口；此时场景
        /// 不会归还给调用方。只有可观察操作以 Failed 终态完成时，所有权才归还调用方。
        /// 本方法还必须以返回值作为原子所有权边界：抛出异常或返回 null 时不得已经发起卸载，
        /// 场景仍归调用方；一旦返回非 null 操作，运行期间及操作被提前释放后的收口均归后端。
        /// </remarks>
        IMSceneFlowOperation UnloadAsync(IMSceneFlowScene scene);

        /// <summary>把场景设置为 Unity Active Scene；失败时应抛出异常。</summary>
        void SetActiveScene(IMSceneFlowScene scene);
    }

    /// <summary>传给每个场景阶段命令的只读上下文。</summary>
    public sealed class MSceneFlowContext
    {
        internal MSceneFlowContext(
            MSceneFlowPlan plan,
            MSceneFlowRequest request,
            IMSceneFlowScene previousScene)
        {
            Plan = plan;
            Request = request;
            PreviousScene = previousScene;
        }

        /// <summary>获取此次切换使用的冻结计划。</summary>
        public MSceneFlowPlan Plan { get; }

        /// <summary>获取可用于观察状态或请求取消的切换请求。</summary>
        public MSceneFlowRequest Request { get; }

        /// <summary>获取切换开始时由 MSceneFlow 管理的旧场景；首次加载时为 null。</summary>
        public IMSceneFlowScene PreviousScene { get; }

        /// <summary>
        /// 获取已经加载的目标场景；在 <see cref="MSceneFlowStage.AfterLoad"/> 之前为 null。
        /// 所有权属于 MSceneFlow，命令不得自行释放。
        /// </summary>
        public IMSceneFlowScene TargetScene { get; internal set; }

        /// <summary>获取调用方随计划传入的业务参数。</summary>
        public object Arguments => Plan.Arguments;
    }

    internal static class MSceneFlowStageUtility
    {
        public static bool IsCommandStage(MSceneFlowStage stage)
        {
            return stage == MSceneFlowStage.BeforeTransition
                || stage == MSceneFlowStage.BeforeLoad
                || stage == MSceneFlowStage.AfterLoad
                || stage == MSceneFlowStage.BeforeActivation
                || stage == MSceneFlowStage.AfterActivation
                || stage == MSceneFlowStage.BeforeSetActiveScene
                || stage == MSceneFlowStage.AfterSetActiveScene
                || stage == MSceneFlowStage.BeforeUnload
                || stage == MSceneFlowStage.AfterUnload
                || stage == MSceneFlowStage.AfterTransition
                || stage == MSceneFlowStage.Finally;
        }
    }
}
