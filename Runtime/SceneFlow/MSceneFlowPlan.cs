using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 配置一次可重复执行的内容场景切换，以及各确定性阶段要运行的命令。
    /// </summary>
    /// <remarks>
    /// 计划第一次交给 <see cref="MSceneFlow.Transition(MSceneFlowPlan)"/> 时会被冻结。每个命令工厂必须在每次执行时
    /// 返回一个全新的 <see cref="IMActionNode"/>；同一阶段严格按注册顺序串行执行。
    /// </remarks>
    public sealed class MSceneFlowPlan
    {
        private readonly Dictionary<MSceneFlowStage, List<MSceneFlowCommandRegistration>> _commands =
            new Dictionary<MSceneFlowStage, List<MSceneFlowCommandRegistration>>();
        private MSceneFlowOrder _order = MSceneFlowOrder.LoadThenUnload;
        private MTimeMode _commandTimeMode = MTimeMode.Unscaled;
        private int _priority = 100;
        private bool _setActiveScene = true;
        private object _arguments;

        /// <summary>创建一个以 Addressables 地址、AssetReference 或后端自定义键为目标的计划。</summary>
        /// <param name="targetKey">目标场景键，不能为 null 或空字符串。</param>
        public MSceneFlowPlan(object targetKey)
        {
            if (targetKey == null)
            {
                throw new ArgumentNullException(nameof(targetKey));
            }

            string textKey = targetKey as string;
            if (textKey != null && string.IsNullOrWhiteSpace(textKey))
            {
                throw new ArgumentException("场景切换的目标字符串键不能为空或纯空白字符。", nameof(targetKey));
            }

            TargetKey = targetKey;
        }

        /// <summary>获取目标场景键。</summary>
        public object TargetKey { get; }

        /// <summary>获取计划是否已经被冻结。</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>获取或设置加载与卸载顺序；默认先加载后卸载。</summary>
        public MSceneFlowOrder Order
        {
            get => _order;
            set
            {
                EnsureMutable();
                if (!Enum.IsDefined(typeof(MSceneFlowOrder), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _order = value;
            }
        }

        /// <summary>获取或设置用户命令使用的时间域；默认使用非缩放时间。</summary>
        public MTimeMode CommandTimeMode
        {
            get => _commandTimeMode;
            set
            {
                EnsureMutable();
                if (!Enum.IsDefined(typeof(MTimeMode), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _commandTimeMode = value;
            }
        }

        /// <summary>获取或设置底层场景加载优先级。</summary>
        public int Priority
        {
            get => _priority;
            set
            {
                EnsureMutable();
                _priority = value;
            }
        }

        /// <summary>获取或设置激活后是否把目标设为 Unity Active Scene；默认设置。</summary>
        public bool SetActiveScene
        {
            get => _setActiveScene;
            set
            {
                EnsureMutable();
                _setActiveScene = value;
            }
        }

        /// <summary>获取或设置传给所有阶段命令的项目自定义参数。</summary>
        public object Arguments
        {
            get => _arguments;
            set
            {
                EnsureMutable();
                _arguments = value;
            }
        }

        /// <summary>
        /// 在一个可执行命令的阶段注册动作工厂。
        /// </summary>
        /// <param name="stage">Before/After 或 Finally 命令点。</param>
        /// <param name="commandFactory">接收当前上下文并返回全新动作节点的工厂。</param>
        /// <param name="name">可选诊断名称；省略时使用注册序号。</param>
        /// <returns>当前计划，便于链式配置。</returns>
        public MSceneFlowPlan At(
            MSceneFlowStage stage,
            Func<MSceneFlowContext, IMActionNode> commandFactory,
            string name = null)
        {
            EnsureMutable();
            if (!MSceneFlowStageUtility.IsCommandStage(stage))
            {
                throw new ArgumentException("只能在 Before、After 或 Finally 阶段注册命令。", nameof(stage));
            }

            if (commandFactory == null)
            {
                throw new ArgumentNullException(nameof(commandFactory));
            }

            List<MSceneFlowCommandRegistration> stageCommands;
            if (!_commands.TryGetValue(stage, out stageCommands))
            {
                stageCommands = new List<MSceneFlowCommandRegistration>();
                _commands.Add(stage, stageCommands);
            }

            string resolvedName = string.IsNullOrWhiteSpace(name)
                ? stage + " #" + (stageCommands.Count + 1)
                : name;
            stageCommands.Add(new MSceneFlowCommandRegistration(resolvedName, commandFactory));
            return this;
        }

        /// <summary>注册不需要读取场景上下文的动作工厂。</summary>
        public MSceneFlowPlan At(
            MSceneFlowStage stage,
            Func<IMActionNode> commandFactory,
            string name = null)
        {
            if (commandFactory == null)
            {
                throw new ArgumentNullException(nameof(commandFactory));
            }

            return At(stage, _ => commandFactory(), name);
        }

        /// <summary>注册一个在指定阶段立即执行的同步回调。</summary>
        public MSceneFlowPlan Do(
            MSceneFlowStage stage,
            Action<MSceneFlowContext> command,
            string name = null)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return At(stage, context => MAction.Callback(() => command(context)), name);
        }

        internal void Freeze()
        {
            IsFrozen = true;
        }

        internal IReadOnlyList<MSceneFlowCommandRegistration> GetCommands(MSceneFlowStage stage)
        {
            List<MSceneFlowCommandRegistration> commands;
            return _commands.TryGetValue(stage, out commands)
                ? commands
                : Array.Empty<MSceneFlowCommandRegistration>();
        }

        private void EnsureMutable()
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException("场景切换计划在首次启动后即被冻结，不能再次修改。");
            }
        }
    }

    internal sealed class MSceneFlowCommandRegistration
    {
        public MSceneFlowCommandRegistration(
            string name,
            Func<MSceneFlowContext, IMActionNode> factory)
        {
            Name = name;
            Factory = factory;
        }

        public string Name { get; }
        public Func<MSceneFlowContext, IMActionNode> Factory { get; }
    }
}
