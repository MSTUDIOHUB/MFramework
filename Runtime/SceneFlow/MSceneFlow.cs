using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 串行执行一个可观察、可取消且拥有明确场景所有权的内容场景切换。
    /// </summary>
    /// <remarks>
    /// MSceneFlow 只管理一个 Additive 内容场景；承载 MGameEntry 的 Bootstrap 场景必须常驻。
    /// Fade、输入锁、出生点、镜头和游戏模式等项目逻辑应注册为阶段命令。
    /// </remarks>
    public sealed class MSceneFlow : IMFrameworkModule, IMFrameworkTickable, IDisposable
    {
        private readonly IMSceneFlowBackend _backend;
        private Execution _activeExecution;
        private IMSceneFlowScene _currentScene;
        private bool _isShutdown;

        /// <summary>创建使用 Addressables 的默认场景流。</summary>
        public MSceneFlow()
            : this(new MAddressablesSceneFlowBackend())
        {
        }

        /// <summary>创建使用指定可替换后端的场景流。</summary>
        /// <param name="backend">负责加载、激活、卸载和设置 Active Scene 的后端。</param>
        public MSceneFlow(IMSceneFlowBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>获取当前是否有场景切换正在执行。</summary>
        public bool IsBusy => _activeExecution != null;

        /// <summary>获取当前由场景流持有的内容场景；首次加载前为 null。</summary>
        public IMSceneFlowScene CurrentScene => _currentScene;

        /// <summary>获取当前切换请求；没有正在执行的切换时为 null。</summary>
        public MSceneFlowRequest ActiveRequest => _activeExecution?.Request;

        /// <summary>
        /// 接管一个已经由相同后端加载的初始内容场景。
        /// </summary>
        /// <exception cref="InvalidOperationException">正在切换、已有当前场景，或场景已经释放。</exception>
        public void AdoptCurrentScene(IMSceneFlowScene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            EnsureAvailable();
            if (_currentScene != null)
            {
                throw new InvalidOperationException("MSceneFlow 已经持有一个当前内容场景。");
            }

            if (scene.IsReleased)
            {
                throw new InvalidOperationException("MSceneFlow 不能接管已经释放的场景。");
            }

            _currentScene = scene;
        }

        /// <summary>
        /// 把当前内容场景所有权转移给调用方。仅应在框架外部接管生命周期时使用。
        /// </summary>
        public IMSceneFlowScene TakeCurrentScene()
        {
            EnsureAvailable();
            IMSceneFlowScene scene = _currentScene;
            _currentScene = null;
            return scene;
        }

        /// <summary>
        /// 使用默认配置直接切换到指定目标场景（适合无需自定义阶段命令的快捷场景切换）。
        /// </summary>
        /// <param name="targetKey">目标场景键（Addressables 地址或 AssetReference）。</param>
        /// <param name="order">加载与卸载顺序；默认先加载后卸载。</param>
        /// <returns>可观察或取消此次切换的请求。</returns>
        public MSceneFlowRequest Transition(object targetKey, MSceneFlowOrder order = MSceneFlowOrder.LoadThenUnload)
        {
            MSceneFlowPlan plan = new MSceneFlowPlan(targetKey)
            {
                Order = order
            };
            return Transition(plan);
        }

        /// <summary>开始执行一个已经配置好的场景切换计划。</summary>
        /// <param name="plan">目标、策略和阶段命令。</param>
        /// <returns>可观察或取消此次切换的请求。</returns>
        /// <exception cref="InvalidOperationException">模块已关闭、正在切换，或当前场景已被外部释放。</exception>
        public MSceneFlowRequest Transition(MSceneFlowPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            EnsureAvailable();
            IMSceneFlowScene previousScene = _currentScene;
            if (previousScene != null && previousScene.IsReleased)
            {
                throw new InvalidOperationException("当前内容场景已在 MSceneFlow 外部被释放。");
            }

            plan.Freeze();
            MSceneFlowRequest request = new MSceneFlowRequest(this, plan, previousScene);
            Execution execution = new Execution(this, _backend, plan, request, previousScene);
            _activeExecution = execution;
            execution.Start();
            return request;
        }

        /// <inheritdoc />
        public void Initialize(MFrameworkApp app)
        {
            _isShutdown = false;
        }

        /// <inheritdoc />
        public void Tick(MFrameTime time)
        {
            _activeExecution?.Tick(time);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (_isShutdown)
            {
                return;
            }

            _isShutdown = true;
            List<Exception> exceptions = null;

            Execution active = _activeExecution;
            if (active != null)
            {
                try
                {
                    active.AbortForShutdown();
                }
                catch (Exception exception)
                {
                    AddException(ref exceptions, exception);
                }
            }

            List<IMSceneFlowScene> scenes = new List<IMSceneFlowScene>();
            AddUniqueScene(scenes, _currentScene);
            if (active != null)
            {
                AddUniqueScene(scenes, active.PreviousScene);
                AddUniqueScene(scenes, active.TargetScene);
            }

            _currentScene = null;
            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!scenes[i].IsReleased)
                    {
                        scenes[i].Dispose();
                    }
                }
                catch (Exception exception)
                {
                    AddException(ref exceptions, exception);
                }
            }

            if (exceptions != null)
            {
                throw new AggregateException("关闭 MSceneFlow 时有一个或多个场景资源释放失败。", exceptions);
            }
        }

        /// <summary>关闭场景流并释放当前内容场景。</summary>
        public void Dispose()
        {
            Shutdown();
        }

        internal bool Cancel(MSceneFlowRequest request)
        {
            return _activeExecution != null
                && ReferenceEquals(_activeExecution.Request, request)
                && _activeExecution.RequestCancellation();
        }

        private void EnsureAvailable()
        {
            if (_isShutdown)
            {
                throw new InvalidOperationException("MSceneFlow 已关闭，不能继续使用。");
            }

            if (_activeExecution != null)
            {
                throw new InvalidOperationException("MSceneFlow 当前已有正在执行的场景切换，请等待其完成后再发起新切换。");
            }
        }

        private void SetCurrentScene(IMSceneFlowScene scene)
        {
            _currentScene = scene;
        }

        private void Detach(Execution execution)
        {
            if (ReferenceEquals(_activeExecution, execution))
            {
                _activeExecution = null;
            }
        }

        private static void AddUniqueScene(List<IMSceneFlowScene> scenes, IMSceneFlowScene scene)
        {
            if (scene == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Count; i++)
            {
                if (ReferenceEquals(scenes[i], scene))
                {
                    return;
                }
            }

            scenes.Add(scene);
        }

        private static void AddException(ref List<Exception> exceptions, Exception exception)
        {
            if (exceptions == null)
            {
                exceptions = new List<Exception>();
            }

            exceptions.Add(exception);
        }

        private sealed class Execution
        {
            private readonly MSceneFlow _owner;
            private readonly IMSceneFlowBackend _backend;
            private readonly MSceneFlowPlan _plan;
            private readonly MSceneFlowContext _context;
            private readonly MSceneFlowStage[] _stages;
            private int _stageIndex = -1;
            private IReadOnlyList<MSceneFlowCommandRegistration> _stageCommands;
            private int _commandIndex;
            private IMActionNode _command;
            private IMSceneFlowOperation _operation;
            private OperationKind _operationKind;
            private bool _isAdvancing;
            private bool _isTicking;
            private bool _isTerminal;
            private bool _isFinalizing;
            private bool _cancellationRequested;
            private bool _targetPromoted;
            private bool _targetWasSetActive;
            private MSceneFlowState _pendingOutcome = MSceneFlowState.Succeeded;
            private Exception _pendingFailure;
            private MSceneFlowStage _failedStage = MSceneFlowStage.None;

            public Execution(
                MSceneFlow owner,
                IMSceneFlowBackend backend,
                MSceneFlowPlan plan,
                MSceneFlowRequest request,
                IMSceneFlowScene previousScene)
            {
                _owner = owner;
                _backend = backend;
                _plan = plan;
                Request = request;
                PreviousScene = previousScene;
                _context = new MSceneFlowContext(plan, request, previousScene);
                _stages = BuildStages(plan, previousScene != null);
            }

            public MSceneFlowRequest Request { get; }
            public IMSceneFlowScene PreviousScene { get; }
            public IMSceneFlowScene TargetScene => _context.TargetScene;

            public void Start()
            {
                AdvanceToNextStage();
            }

            public void Tick(MFrameTime time)
            {
                if (_isTerminal || _isTicking)
                {
                    return;
                }

                _isTicking = true;
                try
                {
                    if (_operation != null)
                    {
                        PollOperation();
                        return;
                    }

                    if (_command == null)
                    {
                        return;
                    }

                    float deltaTime = _plan.CommandTimeMode == MTimeMode.Unscaled
                        ? time.UnscaledDeltaTime
                        : time.DeltaTime;
                    IMActionNode tickingCommand = _command;
                    try
                    {
                        tickingCommand.Tick(deltaTime);
                    }
                    catch (Exception exception)
                    {
                        if (!_isTerminal && ReferenceEquals(_command, tickingCommand))
                        {
                            HandleCommandFailure(exception);
                        }

                        return;
                    }

                    if (_isTerminal || !ReferenceEquals(_command, tickingCommand))
                    {
                        return;
                    }

                    if (_cancellationRequested && !_isFinalizing)
                    {
                        CancelCurrentCommand();
                        BeginCancellation();
                        return;
                    }

                    if (tickingCommand.Status == MActionStatus.Completed)
                    {
                        _command = null;
                        _commandIndex++;
                        StartNextCommand();
                    }
                    else if (tickingCommand.Status == MActionStatus.Cancelled && _cancellationRequested && !_isFinalizing)
                    {
                        _command = null;
                        BeginCancellation();
                    }
                    else if (tickingCommand.Status != MActionStatus.Running)
                    {
                        HandleCommandFailure(tickingCommand.Failure ?? new InvalidOperationException("场景阶段命令未正常完成即结束。"));
                    }
                }
                finally
                {
                    _isTicking = false;
                }
            }

            public bool RequestCancellation()
            {
                if (_isTerminal || _isFinalizing || _cancellationRequested)
                {
                    return false;
                }

                _cancellationRequested = true;
                Request.MarkCancelling();

                if (_operation != null)
                {
                    PollOperation();
                    return true;
                }

                CancelCurrentCommand();
                BeginCancellation();
                return true;
            }

            public void AbortForShutdown()
            {
                if (_isTerminal)
                {
                    return;
                }

                _isTerminal = true;
                _owner.Detach(this);

                Exception failure = _pendingFailure;
                IMActionNode command = _command;
                _command = null;
                try
                {
                    command?.Cancel();
                }
                catch (Exception exception)
                {
                    failure = AppendFailure(failure, exception);
                }

                IMSceneFlowOperation operation = _operation;
                _operation = null;
                try
                {
                    operation?.Dispose();
                }
                catch (Exception exception)
                {
                    failure = AppendFailure(failure, exception);
                }

                Request.Failure = failure;
                if (failure != null && Request.FailedStage == MSceneFlowStage.None)
                {
                    Request.FailedStage = Request.Stage;
                }

                Request.Finish(
                    failure == null ? MSceneFlowState.Cancelled : MSceneFlowState.Failed,
                    failure == null ? MSceneFlowStage.Cancelled : MSceneFlowStage.Failed);

                if (failure != null)
                {
                    throw failure;
                }
            }

            private void AdvanceToNextStage()
            {
                if (_isTerminal || _isFinalizing || _isAdvancing)
                {
                    return;
                }

                _isAdvancing = true;
                try
                {
                    while (!_isTerminal && !_isFinalizing && _operation == null && _command == null)
                    {
                        if (_cancellationRequested)
                        {
                            BeginCancellation();
                            return;
                        }

                        _stageIndex++;
                        if (_stageIndex >= _stages.Length)
                        {
                            BeginFinalization(MSceneFlowState.Succeeded, null, MSceneFlowStage.None);
                            return;
                        }

                        Request.SetProgress((float)_stageIndex / Math.Max(1, _stages.Length));
                        EnterStage(_stages[_stageIndex]);
                    }
                }
                catch (Exception exception)
                {
                    BeginFailure(exception, Request.Stage);
                }
                finally
                {
                    _isAdvancing = false;
                }
            }

            private void EnterStage(MSceneFlowStage stage)
            {
                Request.SetStage(stage);
                if (_isTerminal)
                {
                    return;
                }

                if (_cancellationRequested)
                {
                    BeginCancellation();
                    return;
                }

                if (MSceneFlowStageUtility.IsCommandStage(stage))
                {
                    _stageCommands = _plan.GetCommands(stage);
                    _commandIndex = 0;
                    StartNextCommand();
                    return;
                }

                switch (stage)
                {
                    case MSceneFlowStage.Loading:
                        BeginLoad();
                        break;
                    case MSceneFlowStage.Activating:
                        BeginActivation();
                        break;
                    case MSceneFlowStage.SettingActiveScene:
                        _backend.SetActiveScene(_context.TargetScene);
                        _targetWasSetActive = true;
                        break;
                    case MSceneFlowStage.Unloading:
                        BeginUnload();
                        break;
                    default:
                        throw new InvalidOperationException("MSceneFlow 进入了不支持的操作阶段：" + stage);
                }
            }

            private void StartNextCommand()
            {
                while (!_isTerminal && _operation == null && _command == null)
                {
                    if (_cancellationRequested && !_isFinalizing)
                    {
                        BeginCancellation();
                        return;
                    }

                    if (_commandIndex >= _stageCommands.Count)
                    {
                        if (_isFinalizing)
                        {
                            CompleteTerminal();
                        }
                        else
                        {
                            AdvanceToNextStage();
                        }

                        return;
                    }

                    MSceneFlowCommandRegistration registration = _stageCommands[_commandIndex];
                    MSceneFlowStage commandStage = Request.Stage;
                    bool wasFinalizing = _isFinalizing;
                    Request.SetCurrentCommand(registration.Name);
                    if (_isTerminal || Request.Stage != commandStage || _isFinalizing != wasFinalizing)
                    {
                        return;
                    }

                    if (_cancellationRequested && !_isFinalizing)
                    {
                        BeginCancellation();
                        return;
                    }

                    IMActionNode node = null;
                    try
                    {
                        node = registration.Factory(_context);
                        if (_isTerminal || Request.Stage != commandStage || _isFinalizing != wasFinalizing || _command != null)
                        {
                            return;
                        }

                        if (node == null)
                        {
                            throw new InvalidOperationException($"场景阶段命令 '{registration.Name}' 返回了 null。");
                        }

                        if (node.Status != MActionStatus.Created)
                        {
                            throw new InvalidOperationException($"场景阶段命令 '{registration.Name}' 必须返回全新的动作节点（Created 状态）。");
                        }

                        if (_cancellationRequested && !_isFinalizing)
                        {
                            node.Cancel();
                            BeginCancellation();
                            return;
                        }

                        _command = node;
                        node.Start();
                    }
                    catch (Exception exception)
                    {
                        if (!_isTerminal
                            && Request.Stage == commandStage
                            && _isFinalizing == wasFinalizing
                            && (node == null || _command == null || ReferenceEquals(_command, node)))
                        {
                            HandleCommandFailure(exception);
                        }

                        return;
                    }

                    if (_isTerminal || !ReferenceEquals(_command, node))
                    {
                        return;
                    }

                    if (_cancellationRequested && !_isFinalizing)
                    {
                        CancelCurrentCommand();
                        BeginCancellation();
                        return;
                    }

                    if (node.Status == MActionStatus.Completed)
                    {
                        _command = null;
                        _commandIndex++;
                        continue;
                    }

                    if (node.Status != MActionStatus.Running)
                    {
                        HandleCommandFailure(node.Failure ?? new InvalidOperationException($"场景阶段命令 '{registration.Name}' 启动后未进入 Running 或 Completed 状态。"));
                    }

                    return;
                }
            }

            private void HandleCommandFailure(Exception exception)
            {
                _command = null;
                if (_isFinalizing)
                {
                    RecordFailure(exception, MSceneFlowStage.Finally);
                    _commandIndex++;
                    StartNextCommand();
                    return;
                }

                BeginFailure(exception, Request.Stage);
            }

            private void BeginLoad()
            {
                _operationKind = OperationKind.Load;
                _operation = _backend.LoadAsync(_plan.TargetKey, _plan.Priority)
                    ?? throw new InvalidOperationException("场景后端返回了 null 加载操作。");
                PollOperation();
            }

            private void BeginActivation()
            {
                if (_context.TargetScene == null)
                {
                    throw new InvalidOperationException("目标场景尚未加载，无法开始激活。");
                }

                _operationKind = OperationKind.Activate;
                _operation = _backend.ActivateAsync(_context.TargetScene)
                    ?? throw new InvalidOperationException("场景后端返回了 null 激活操作。");
                PollOperation();
            }

            private void BeginUnload()
            {
                if (PreviousScene == null)
                {
                    throw new InvalidOperationException("不存在旧场景，无法执行卸载。");
                }

                _operationKind = OperationKind.Unload;
                _operation = _backend.UnloadAsync(PreviousScene)
                    ?? throw new InvalidOperationException("场景后端返回了 null 卸载操作。");

                Request.HasCommitted = true;
                if (_plan.Order == MSceneFlowOrder.LoadThenUnload)
                {
                    PromoteTarget();
                }
                else
                {
                    _owner.SetCurrentScene(null);
                }

                PollOperation();
            }

            private void PollOperation()
            {
                if (_operation == null || _isTerminal)
                {
                    return;
                }

                IMSceneFlowOperation operation = _operation;
                try
                {
                    float operationProgress = operation.Progress;
                    bool isDone = operation.IsDone;
                    if (_isTerminal || !ReferenceEquals(_operation, operation))
                    {
                        return;
                    }

                    Request.SetProgress((_stageIndex + operationProgress) / Math.Max(1, _stages.Length));
                    if (isDone)
                    {
                        CompleteOperation();
                    }
                }
                catch (Exception exception)
                {
                    if (_isTerminal || !ReferenceEquals(_operation, operation))
                    {
                        return;
                    }

                    if (_isFinalizing && _operationKind == OperationKind.CleanupTarget)
                    {
                        DisposeCurrentOperation();
                        RecordFailure(exception, MSceneFlowStage.CleaningTarget);
                        BeginFinallyCommands();
                    }
                    else if (_operationKind == OperationKind.Unload)
                    {
                        RecordFailure(exception, Request.Stage);
                        DisposeCurrentOperation();
                        BeginFinalization(MSceneFlowState.Failed, _pendingFailure, _failedStage);
                    }
                    else
                    {
                        BeginFailure(exception, Request.Stage);
                    }
                }
            }

            private void CompleteOperation()
            {
                IMSceneFlowOperation operation = _operation;
                OperationKind operationKind = _operationKind;
                _operation = null;

                Exception failure = null;
                IMSceneFlowScene loadedScene = null;
                try
                {
                    failure = operation.Failure;
                    if (!_isTerminal && operationKind == OperationKind.Load && failure == null)
                    {
                        IMSceneFlowLoadOperation loadOperation = operation as IMSceneFlowLoadOperation;
                        if (loadOperation == null)
                        {
                            failure = new InvalidOperationException("加载操作未实现 IMSceneFlowLoadOperation 接口。");
                        }
                        else
                        {
                            loadedScene = loadOperation.TakeScene();
                            if (loadedScene == null)
                            {
                                failure = new InvalidOperationException("已完成的场景加载操作返回了 null 目标场景。");
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    failure = AppendFailure(failure, exception);
                }

                try
                {
                    operation.Dispose();
                }
                catch (Exception exception)
                {
                    failure = AppendFailure(failure, exception);
                }

                if (_isTerminal)
                {
                    loadedScene?.Dispose();
                    return;
                }

                if (loadedScene != null)
                {
                    _context.TargetScene = loadedScene;
                    Request.TargetScene = loadedScene;
                }

                if (operationKind == OperationKind.CleanupTarget)
                {
                    if (failure != null)
                    {
                        RecordFailure(failure, MSceneFlowStage.CleaningTarget);
                        TryDisposeSceneSafely(_context.TargetScene);
                    }

                    BeginFinallyCommands();
                    return;
                }

                if (operationKind == OperationKind.Unload && failure != null)
                {
                    RecordFailure(failure, Request.Stage);
                    TryDisposeSceneSafely(PreviousScene);
                    BeginFinalization(MSceneFlowState.Failed, _pendingFailure, _failedStage);
                    return;
                }

                if (_cancellationRequested)
                {
                    if (failure != null)
                    {
                        RecordFailure(failure, Request.Stage);
                        BeginFinalization(MSceneFlowState.Failed, _pendingFailure, _failedStage);
                    }
                    else
                    {
                        BeginCancellation();
                    }

                    return;
                }

                if (failure != null)
                {
                    BeginFailure(failure, Request.Stage);
                    return;
                }

                if (operationKind == OperationKind.Activate
                    && (PreviousScene == null || _plan.Order == MSceneFlowOrder.UnloadThenLoad))
                {
                    PromoteTarget();
                }

                AdvanceToNextStage();
            }

            private void PromoteTarget()
            {
                if (_targetPromoted)
                {
                    return;
                }

                if (_context.TargetScene == null)
                {
                    throw new InvalidOperationException("目标场景为空，无法提交为当前内容场景。");
                }

                _targetPromoted = true;
                Request.HasCommitted = true;
                _owner.SetCurrentScene(_context.TargetScene);
            }

            private void BeginFailure(Exception exception, MSceneFlowStage stage)
            {
                if (_isTerminal)
                {
                    return;
                }

                RecordFailure(exception, stage);
                BeginFinalization(MSceneFlowState.Failed, _pendingFailure, _failedStage);
            }

            private void BeginCancellation()
            {
                if (_isTerminal || _isFinalizing)
                {
                    return;
                }

                BeginFinalization(MSceneFlowState.Cancelled, null, MSceneFlowStage.None);
            }

            private void BeginFinalization(
                MSceneFlowState outcome,
                Exception failure,
                MSceneFlowStage failedStage)
            {
                if (_isTerminal || _isFinalizing)
                {
                    return;
                }

                _pendingOutcome = outcome;
                if (failure != null)
                {
                    _pendingFailure = failure;
                    _failedStage = failedStage;
                }

                _isFinalizing = true;
                CancelCurrentCommand();
                DisposeCurrentOperation();

                if (TryBeginUncommittedTargetCleanup())
                {
                    return;
                }

                BeginFinallyCommands();
            }

            private void BeginFinallyCommands()
            {
                if (_isTerminal)
                {
                    return;
                }

                Request.Failure = _pendingFailure;
                Request.FailedStage = _failedStage;
                Request.SetProgress(0.99f);
                Request.SetStage(MSceneFlowStage.Finally);
                _stageCommands = _plan.GetCommands(MSceneFlowStage.Finally);
                _commandIndex = 0;
                StartNextCommand();
            }

            private bool TryBeginUncommittedTargetCleanup()
            {
                if (_targetPromoted || _context.TargetScene == null)
                {
                    return false;
                }

                if (_targetWasSetActive && PreviousScene != null && !PreviousScene.IsReleased)
                {
                    try
                    {
                        _backend.SetActiveScene(PreviousScene);
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(exception, Request.Stage);
                    }
                }

                try
                {
                    if (_context.TargetScene.IsReleased)
                    {
                        return false;
                    }

                    Request.SetStage(MSceneFlowStage.CleaningTarget);
                    if (_isTerminal)
                    {
                        return true;
                    }

                    _operationKind = OperationKind.CleanupTarget;
                    _operation = _backend.UnloadAsync(_context.TargetScene)
                        ?? throw new InvalidOperationException("场景后端返回了 null 清理操作。");
                    PollOperation();
                    return true;
                }
                catch (Exception exception)
                {
                    RecordFailure(exception, MSceneFlowStage.CleaningTarget);
                    TryDisposeSceneSafely(_context.TargetScene);
                    return false;
                }
            }

            private void TryDisposeSceneSafely(IMSceneFlowScene scene)
            {
                if (scene == null)
                {
                    return;
                }

                try
                {
                    if (!scene.IsReleased)
                    {
                        scene.Dispose();
                    }
                }
                catch (Exception exception)
                {
                    RecordFailure(exception, Request.Stage);
                }
            }

            private void CancelCurrentCommand()
            {
                if (_command == null)
                {
                    return;
                }

                IMActionNode command = _command;
                _command = null;
                try
                {
                    command.Cancel();
                }
                catch (Exception exception)
                {
                    RecordFailure(exception, Request.Stage);
                }
            }

            private void DisposeCurrentOperation()
            {
                if (_operation == null)
                {
                    return;
                }

                IMSceneFlowOperation operation = _operation;
                _operation = null;
                try
                {
                    operation.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(exception, Request.Stage);
                }
            }

            private void RecordFailure(Exception exception, MSceneFlowStage stage)
            {
                if (_isTerminal)
                {
                    return;
                }

                if (exception == null)
                {
                    exception = new InvalidOperationException("MSceneFlow 发生未知错误。");
                }

                if (_pendingFailure == null)
                {
                    _pendingFailure = exception;
                    _failedStage = stage;
                }
                else if (!ReferenceEquals(_pendingFailure, exception))
                {
                    _pendingFailure = Combine(_pendingFailure, exception);
                }

                _pendingOutcome = MSceneFlowState.Failed;
                Request.Failure = _pendingFailure;
                Request.FailedStage = _failedStage;
            }

            private void CompleteTerminal()
            {
                if (_isTerminal)
                {
                    return;
                }

                _isTerminal = true;
                _isFinalizing = false;
                Request.Failure = _pendingFailure;
                Request.FailedStage = _failedStage;
                _owner.Detach(this);

                MSceneFlowState outcome = _pendingFailure == null
                    ? _pendingOutcome
                    : MSceneFlowState.Failed;
                MSceneFlowStage terminalStage = outcome == MSceneFlowState.Succeeded
                    ? MSceneFlowStage.Completed
                    : (outcome == MSceneFlowState.Cancelled
                        ? MSceneFlowStage.Cancelled
                        : MSceneFlowStage.Failed);
                Request.Finish(outcome, terminalStage);
            }

            private static MSceneFlowStage[] BuildStages(MSceneFlowPlan plan, bool hasPreviousScene)
            {
                List<MSceneFlowStage> stages = new List<MSceneFlowStage>();
                stages.Add(MSceneFlowStage.BeforeTransition);

                if (plan.Order == MSceneFlowOrder.UnloadThenLoad && hasPreviousScene)
                {
                    AddUnloadStages(stages);
                }

                stages.Add(MSceneFlowStage.BeforeLoad);
                stages.Add(MSceneFlowStage.Loading);
                stages.Add(MSceneFlowStage.AfterLoad);
                stages.Add(MSceneFlowStage.BeforeActivation);
                stages.Add(MSceneFlowStage.Activating);
                stages.Add(MSceneFlowStage.AfterActivation);

                if (plan.SetActiveScene)
                {
                    stages.Add(MSceneFlowStage.BeforeSetActiveScene);
                    stages.Add(MSceneFlowStage.SettingActiveScene);
                    stages.Add(MSceneFlowStage.AfterSetActiveScene);
                }

                if (plan.Order == MSceneFlowOrder.LoadThenUnload && hasPreviousScene)
                {
                    AddUnloadStages(stages);
                }

                stages.Add(MSceneFlowStage.AfterTransition);
                return stages.ToArray();
            }

            private static void AddUnloadStages(List<MSceneFlowStage> stages)
            {
                stages.Add(MSceneFlowStage.BeforeUnload);
                stages.Add(MSceneFlowStage.Unloading);
                stages.Add(MSceneFlowStage.AfterUnload);
            }

            private static Exception Combine(Exception first, Exception second)
            {
                AggregateException aggregate = first as AggregateException;
                if (aggregate != null)
                {
                    List<Exception> exceptions = new List<Exception>(aggregate.InnerExceptions);
                    exceptions.Add(second);
                    return new AggregateException("MSceneFlow 在切换过程中遇到多个错误。", exceptions);
                }

                return new AggregateException("MSceneFlow 在切换过程中遇到多个错误。", first, second);
            }

            private static Exception AppendFailure(Exception existing, Exception additional)
            {
                return existing == null ? additional : Combine(existing, additional);
            }

            private enum OperationKind
            {
                Load,
                Activate,
                Unload,
                CleanupTarget
            }
        }
    }
}
