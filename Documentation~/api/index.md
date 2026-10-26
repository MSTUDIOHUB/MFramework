---
title: API 参考（按模块分类）
---

# API 参考导览

为了让你在写代码时只需引用一个 `using MFramework;` 命名空间，同时在查阅文档时又能一眼找到对应模块的类，我们将 `MFramework` 命名空间下的所有公开类型按 **14 个功能模块**进行了分类整理。你可以从左侧分类目录或下方速查表直接跳转。

## 1. Core（入口与生命周期）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MFrameworkApp](MFramework.MFrameworkApp.yml) | Class | 持有框架服务，并驱动正序初始化、每帧 Tick 与逆序关闭 |
| [MGameEntry](MFramework.MGameEntry.yml) | Class | Unity 场景入口抽象基类，连接 MonoBehaviour 与 `MFrameworkApp` |
| [IMFrameworkModule](MFramework.IMFrameworkModule.yml) | Interface | 定义模块的 `Initialize` 与 `Shutdown` 生命周期回调 |
| [IMFrameworkTickable](MFramework.IMFrameworkTickable.yml) | Interface | 定义常规帧更新 `Tick(MFrameTime)` 回调 |
| [IMFrameworkFixedTickable](MFramework.IMFrameworkFixedTickable.yml) | Interface | 定义固定物理时间步长 `FixedTick(float)` 回调 |
| [IMFrameworkLateTickable](MFramework.IMFrameworkLateTickable.yml) | Interface | 定义帧末更新 `LateTick(MFrameTime)` 回调 |
| [MFrameTime](MFramework.MFrameTime.yml) | Struct | 保存缩放时间 `DeltaTime` 与非缩放时间 `UnscaledDeltaTime` |
| [MDisposableBag](MFramework.MDisposableBag.yml) | Class | 集中收集并按后进先出（LIFO）顺序释放一组 `IDisposable` 句柄 |
| [MFrameworkAppState](MFramework.MFrameworkAppState.yml) | Enum | 描述 `MFrameworkApp` 的生命周期状态 |
| [MFrameworkInitializationException](MFramework.MFrameworkInitializationException.yml) | Class | 报告模块初始化失败及回滚异常 |
| [MFrameworkShutdownException](MFramework.MFrameworkShutdownException.yml) | Class | 报告模块关闭异常 |

## 2. Event（零装箱事件总线）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [IMEventBus](MFramework.IMEventBus.yml) | Interface | 类型安全事件总线接口（`Subscribe` / `Publish` / `Clear`） |
| [MEventBus](MFramework.MEventBus.yml) | Class | 基于泛型通道快照复用的零装箱、零 GC 事件总线实现 |
| [MEventDispatchException](MFramework.MEventDispatchException.yml) | Class | 汇总发布事件时一个或多个订阅者抛出的异常 |

## 3. Action（组合动作编排）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MAction](MFramework.MAction.yml) | Class | 创建 `Callback`、`Delay`、`WaitUntil`、`Sequence`、`Parallel`、`Repeat`、`Custom` 节点的静态工厂 |
| [MActionRunner](MFramework.MActionRunner.yml) | Class | 持有并每帧无 GC 推进根动作节点的运行器模块 |
| [MActionHandle](MFramework.MActionHandle.yml) | Class | 用于观察动作状态或取消动作的 `IDisposable` 句柄 |
| [IMActionNode](MFramework.IMActionNode.yml) | Interface | 单次使用动作节点接口 |
| [MActionNode](MFramework.MActionNode.yml) | Class | 动作节点通用生命周期抽象基类 |
| [MActionStatus](MFramework.MActionStatus.yml) | Enum | 动作节点的生命周期状态（`Created` / `Running` / `Completed` / `Cancelled` / `Failed`） |
| [MActionTickResult](MFramework.MActionTickResult.yml) | Enum | 自定义动作每帧返回的运行或完成状态 |
| [MTimeMode](MFramework.MTimeMode.yml) | Enum | 选择使用缩放时间（`Scaled`）还是非缩放时间（`Unscaled`） |

## 4. StateMachine（有限状态机）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MStateMachine&lt;TContext&gt;](MFramework.MStateMachine-1.yml) | Class | 使用共享上下文运行显式注册状态的有限状态机 |
| [IMState&lt;TContext&gt;](MFramework.IMState-1.yml) | Interface | 定义状态的 `Enter`、`Tick`、`Exit` 回调 |

## 5. Pool（GameObject 对象池）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [IMPool](MFramework.IMPool.yml) | Interface | 对象池注册、预热、生成、回收与清理接口 |
| [MPool](MFramework.MPool.yml) | Class | 基于官方 `ObjectPool` 并缓存 `IMPooledGameObject[]` 的多预制体对象池 |
| [MPoolKey](MFramework.MPoolKey.yml) | Struct | 标识对象池已注册预制体的强类型键 |
| [IMPooledGameObject](MFramework.IMPooledGameObject.yml) | Interface | 接收 `OnSpawned()` 与 `OnDespawned()` 重置回调的组件接口 |

## 6. Addressables（资源加载与租约）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MAddressables](MFramework.MAddressables.yml) | Class | 提供资源加载、实例化、场景加载与依赖下载的轻量入口 |
| [MAssetScope](MFramework.MAssetScope.yml) | Class | 统一持有并逆序释放 Addressables 请求与租约的作用域容器 |
| [IMAddressablesRequest](MFramework.IMAddressablesRequest.yml) | Interface | 所有 Addressables 请求共用的只读状态契约 |
| [MAssetLoadRequest&lt;TAsset&gt;](MFramework.MAssetLoadRequest-1.yml) | Class | 异步资源加载请求 |
| [MGameObjectInstantiateRequest](MFramework.MGameObjectInstantiateRequest.yml) | Class | 异步预制体实例化请求 |
| [MAssetDownloadRequest](MFramework.MAssetDownloadRequest.yml) | Class | 依赖资源下载请求 |
| [MSceneLoadRequest](MFramework.MSceneLoadRequest.yml) | Class | 异步场景加载请求（支持 `TakeLease()` 转移所有权） |
| [MSceneUnloadRequest](MFramework.MSceneUnloadRequest.yml) | Class | 可等待的异步场景卸载请求（自动先激活未激活场景再安全卸载） |
| [MAssetLease&lt;TAsset&gt;](MFramework.MAssetLease-1.yml) | Class | 独占持有已加载资源的租约 |
| [MGameObjectInstanceLease](MFramework.MGameObjectInstanceLease.yml) | Class | 独占持有已实例化 `GameObject` 的租约 |
| [MSceneLease](MFramework.MSceneLease.yml) | Class | 独占持有已加载 Addressables 场景的租约 |
| [MAddressablesRequestState](MFramework.MAddressablesRequestState.yml) | Enum | 请求状态（`Pending` / `Succeeded` / `Failed` / `Abandoned`） |
| [MAddressablesError](MFramework.MAddressablesError.yml) | Class | 包含键值、错误说明与原始异常的诊断信息 |

## 7. SceneFlow（确定性场景切换流）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MSceneFlow](MFramework.MSceneFlow.yml) | Class | 串行执行可观察、可取消内容场景切换的核心模块 |
| [MSceneFlowPlan](MFramework.MSceneFlowPlan.yml) | Class | 配置目标场景、加载顺序策略与各阶段 `At/Do` 命令的计划 |
| [MSceneFlowRequest](MFramework.MSceneFlowRequest.yml) | Class | 观察切换阶段、进度或请求取消的句柄 |
| [MSceneFlowContext](MFramework.MSceneFlowContext.yml) | Class | 传递给阶段命令的只读上下文 |
| [MAddressablesSceneFlowBackend](MFramework.MAddressablesSceneFlowBackend.yml) | Class | 基于 `MAddressables` Additive 模式的默认场景流后端 |
| [IMSceneFlowBackend](MFramework.IMSceneFlowBackend.yml) | Interface | 可替换的场景加载/激活/卸载后端接口 |
| [IMSceneFlowScene](MFramework.IMSceneFlowScene.yml) | Interface | 场景流托管的内容场景接口 |
| [IMSceneFlowOperation](MFramework.IMSceneFlowOperation.yml) | Interface | 场景流可轮询异步操作接口 |
| [IMSceneFlowLoadOperation](MFramework.IMSceneFlowLoadOperation.yml) | Interface | 支持转移目标场景所有权的加载操作接口 |
| [MSceneFlowOrder](MFramework.MSceneFlowOrder.yml) | Enum | `LoadThenUnload`（默认）与 `UnloadThenLoad` 切换策略 |
| [MSceneFlowStage](MFramework.MSceneFlowStage.yml) | Enum | 场景切换的 19 个确定性阶段枚举 |
| [MSceneFlowState](MFramework.MSceneFlowState.yml) | Enum | 场景切换请求的生命周期状态 |

## 8. UI（UGUI 分层与路由）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MUIRoot](MFramework.MUIRoot.yml) | Class | 绑定 Page、Popup、Overlay 三个 UGUI 层级并创建 `MUIService` |
| [MUIService](MFramework.MUIService.yml) | Class | 显式注册视图预制体并按类型缓存单实例的服务 |
| [MUIRouter](MFramework.MUIRouter.yml) | Class | 协调页面栈、模态弹窗栈、覆盖层与统一返回行为的路由器 |
| [MUIView](MFramework.MUIView.yml) | Class | 所有 UGUI 视图的生命周期基类 |
| [MUIPage](MFramework.MUIPage.yml) | Class | 可加入页面历史栈的全屏菜单基类 |
| [MUIPopup](MFramework.MUIPopup.yml) | Class | 模态弹窗基类（支持 `OnClosed(MUIPopupResult)`） |
| [MUIOverlay](MFramework.MUIOverlay.yml) | Class | 非模态覆盖层基类（HUD / Loading / Toast） |
| [MUIActionButton](MFramework.MUIActionButton.yml) | Class | 将 UGUI `Button` 点击转换为 `MUIAction` 的组件 |
| [MUIAction](MFramework.MUIAction.yml) | Enum | 五种通用语义操作（`Confirm` / `Cancel` / `Next` / `Previous` / `Back`） |
| [MUIActionResult](MFramework.MUIActionResult.yml) | Struct | 视图处理 `MUIAction` 后返回给路由器的导航指令 |
| [MUIActionResultType](MFramework.MUIActionResultType.yml) | Enum | 路由指令枚举 |
| [MUIPopupResult](MFramework.MUIPopupResult.yml) | Class | 弹窗关闭时返回的确认/取消状态及可选业务值 |

## 9. Config（配置与跨表引用校验）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MConfig](MFramework.MConfig.yml) | Class | 按 `(Type, Id)` 注册、查询并批量校验 `ScriptableObject` 配置 |
| [MConfigAsset](MFramework.MConfigAsset.yml) | Class | 提供稳定 `Id` 与 `Validate` 入口的配置资产基类 |
| [MConfigValidationContext](MFramework.MConfigValidationContext.yml) | Class | 收集字段错误并执行 `RequireReference` / `TryResolve` 跨表校验的上下文 |
| [MConfigValidationError](MFramework.MConfigValidationError.yml) | Struct | 包含来源类型、ID、字段路径与错误说明的值类型记录 |
| [MConfigRegistrationException](MFramework.MConfigRegistrationException.yml) | Class | 配置 ID 为空或重复时的注册异常 |

## 10. Audio（BGM 与受限并发音效）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MAudio](MFramework.MAudio.yml) | Class | 管理 1 路循环 BGM 与固定数量 SFX 并发声部池的音频模块 |
| [MAudioId](MFramework.MAudioId.yml) | Struct | 标识已注册音频的强类型 ID |

## 11. Save（可靠本地完整快照存档）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MSave&lt;T&gt;](MFramework.MSave-1.yml) | Class | 同步完整快照存档服务（支持 `SaveOrOverwrite`、`Save`、`Load`、`RestoreBackup` 与版本迁移） |
| [MFileSaveStorage](MFramework.MFileSaveStorage.yml) | Class | 基于同目录临时文件与 `File.Replace` 原子替换的本地文件后端 |
| [MJsonUtilitySaveSerializer&lt;T&gt;](MFramework.MJsonUtilitySaveSerializer-1.yml) | Class | 基于 Unity `JsonUtility` 的快照序列化适配器 |
| [IMSaveStorage](MFramework.IMSaveStorage.yml) | Interface | 可替换的存档存储后端接口 |
| [IMSaveSerializer&lt;T&gt;](MFramework.IMSaveSerializer-1.yml) | Interface | 快照序列化策略接口 |
| [IMSaveMigration](MFramework.IMSaveMigration.yml) | Interface | 单步数据结构迁移接口 |
| [MSaveResult&lt;T&gt;](MFramework.MSaveResult-1.yml) | Class | 存档读写与恢复操作的不可变结果回执 |
| [MSaveMetadata](MFramework.MSaveMetadata.yml) | Class | 受 SHA-256 摘要保护的存档元数据（版本号、修订号、CommitId） |
| [MSaveRecoveryToken](MFramework.MSaveRecoveryToken.yml) | Class | 绑定主档与备份指纹的两步恢复凭据 |
| [MSaveStatus](MFramework.MSaveStatus.yml) | Enum | 存档操作结果状态枚举 |
| [MSaveSource](MFramework.MSaveSource.yml) | Enum | 数据来源（`Main` 主档 / `Backup` 备份） |
| [MSaveStorageException](MFramework.MSaveStorageException.yml) | Class | 存储后端故障异常 |

## 12. Random（可恢复确定性随机源）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MRandom](MFramework.MRandom.yml) | Class | 基于固定 PCG-XSH-RR 64/32 算法、支持命名流派生与状态恢复的随机数生成器 |
| [IMRandomSource](MFramework.IMRandomSource.yml) | Interface | 可显式注入的随机源接口 |
| [MRandomState](MFramework.MRandomState.yml) | Struct | 可序列化的随机流完整状态结构 |

## 13. Platform（小游戏宿主平台桥接）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MPlatformService](MFramework.MPlatformService.yml) | Class | 统一转发平台调用、合并初始化请求并提供安全降级的平台服务 |
| [IMPlatformBridge](MFramework.IMPlatformBridge.yml) | Interface | 宿主平台最小能力桥接接口 |
| [MEditorPlatformBridge](MFramework.MEditorPlatformBridge.yml) | Class | 基于 `PlayerPrefs` 的编辑器安全回退桥接实现 |
| [MPlatformInitializationResult](MFramework.MPlatformInitializationResult.yml) | Struct | 平台初始化成功或失败结果 |

## 14. Extensions（常用扩展方法）

| 类型 | 种类 | 用途说明 |
| :--- | :--- | :--- |
| [MDisposableExtensions](MFramework.MDisposableExtensions.yml) | Class | 提供 `.AddTo(MDisposableBag)` 与 `.AddTo(MAssetScope)` 链式托管扩展 |
| [MVectorExtensions](MFramework.MVectorExtensions.yml) | Class | 提供 `Vector3.WithX` / `WithY` / `WithZ` 单分量替换扩展 |
| [MRectTransformExtensions](MFramework.MRectTransformExtensions.yml) | Class | 提供 `SetAnchoredPositionX` / `SetAnchoredPositionY` 扩展 |
| [MGameObjectExtensions](MFramework.MGameObjectExtensions.yml) | Class | 提供 `GetOrAddComponent<T>()` 扩展 |
