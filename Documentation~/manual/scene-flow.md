---
title: SceneFlow 模块
---

# SceneFlow：把切场景变成有顺序的事务

## 从项目问题开始

一次正式的场景切换通常不只是 `LoadSceneAsync`。它还会包含输入锁、淡出、加载、激活、设置 Active Scene、出生点定位、卸载旧场景、淡入和失败恢复。如果这些步骤散落在协程与回调中，项目很难回答三个基本问题：

- 某段业务逻辑究竟发生在加载或卸载的前面还是后面？
- 新场景失败时，旧场景是否还在？
- 取消发生后，谁继续持有并清理迟到的 Addressables 结果？

`MSceneFlow` 用显式阶段、串行命令和唯一场景所有权回答这些问题。它不内置 Fade、出生点、Cinemachine 或 GameMode；这些项目逻辑通过阶段命令组合进来。

## 运行前提

`MGameEntry` 必须位于永久保留的 Bootstrap 场景中。`MSceneFlow` 只管理一个以 Addressables Additive 方式加载的内容场景：

```text
Bootstrap（常驻，持有 MGameEntry）
└── Current Content Scene（由 MSceneFlow 独占 Lease）
```

默认后端固定使用：

- `LoadSceneMode.Additive`；
- `activateOnLoad: false`；
- 显式激活；
- 激活完成后按计划决定是否调用 `SceneManager.SetActiveScene`。

因此 `Single` 模式不会绕开阶段 Hook，也不会意外销毁框架入口。

## 快捷切场景 vs 阶段计划切场景

如果小型独立游戏只需要按默认规则切换到目标场景（无需挂载额外阶段命令），可以直接调用一行快捷重载：

```csharp
MSceneFlowRequest request = sceneFlow.Transition("scene.battle");
```

需要挂载淡入淡出、输入锁或出生点定位命令时，使用 `MSceneFlowPlan`：

```csharp
// 先声明各阶段命令；Finally 在成功、失败或取消时都会执行，适合统一收尾。
MSceneFlowPlan plan = new MSceneFlowPlan(targetSceneReference)
    .At(
        MSceneFlowStage.BeforeTransition,
        context => CreateFadeOutAction(),
        "Fade out")
    .Do(
        MSceneFlowStage.AfterSetActiveScene,
        context => ApplySpawnPoint(context.TargetScene.Scene),
        "Apply spawn point")
    .At(
        MSceneFlowStage.Finally,
        context => CreateFadeInAction(),
        "Restore visible screen")
    .Do(
        MSceneFlowStage.Finally,
        context => ReleaseInputLock(),
        "Release input lock");

// Transition 会冻结并执行计划；从这里开始不要再修改 plan。
MSceneFlowRequest request = sceneFlow.Transition(plan);
```

## 两种确定顺序

### LoadThenUnload（默认）

```text
BeforeTransition
→ BeforeLoad → Loading → AfterLoad
→ BeforeActivation → Activating → AfterActivation
→ BeforeSetActiveScene → SettingActiveScene → AfterSetActiveScene
→ BeforeUnload → Unloading → AfterUnload
→ AfterTransition → Finally → Completed
```

旧场景在目标完成加载、激活、Active Scene 设置和对应命令之前仍然存在。此前任一步失败时，框架会恢复旧 Active Scene，并释放未提交的目标。它是失败恢复更可靠的默认策略，代价是两个内容场景会短暂同时存在，内存峰值较高。

### UnloadThenLoad

```text
BeforeTransition
→ BeforeUnload → Unloading → AfterUnload
→ BeforeLoad → Loading → AfterLoad
→ BeforeActivation → Activating → AfterActivation
→ BeforeSetActiveScene → SettingActiveScene → AfterSetActiveScene
→ AfterTransition → Finally → Completed
```

该策略先释放旧内容，适合内存非常紧张的平台。但一旦 `Unloading` 已开始，旧 Scene 实例及运行时状态就不能零成本恢复；后续加载失败时 `CurrentScene` 会是 null，项目应在 `Finally` 或请求完成回调中进入错误页、重试或加载 fallback。

首次加载没有旧内容场景时，两种策略都会完整跳过 `BeforeUnload / Unloading / AfterUnload`，不会执行伪造的卸载 Hook。

## 各命令点的准确含义

| 命令点 | 执行条件与可用数据 |
|---|---|
| `BeforeTransition` | 任何场景操作开始前；适合取得输入锁和淡出 |
| `BeforeLoad` | 调用后端加载目标前；`TargetScene` 仍为 null |
| `AfterLoad` | 目标已加载但尚未激活；`TargetScene` 已可读 |
| `BeforeActivation` | 开始 Unity 激活前 |
| `AfterActivation` | 目标的 Awake/OnEnable 等激活生命周期已经发生 |
| `BeforeSetActiveScene` | 调用 `SetActiveScene` 前；仅在计划启用该选项时执行 |
| `AfterSetActiveScene` | 目标已是 Unity Active Scene；适合出生点与镜头定位 |
| `BeforeUnload` | 后端开始卸载旧场景前；仅在确有旧场景时执行 |
| `AfterUnload` | 旧场景物理卸载完成后 |
| `AfterTransition` | 仅成功路径执行；适合黑场缓冲等成功后表现 |
| `Finally` | 成功、失败和取消都会执行；适合释放输入锁、恢复遮罩等收口 |

## 失败、提交与取消边界

`MSceneFlowRequest.HasCommitted` 表示流程已经越过无法保证恢复旧 Scene 实例的边界：

- `LoadThenUnload` 在旧场景卸载请求成功启动时提交目标；
- `UnloadThenLoad` 在旧场景卸载请求成功启动时越过破坏性边界，目标激活后才成为新的 `CurrentScene`；
- 没有旧场景时，目标激活完成后提交。

提交前失败或取消时，未提交目标会在 `CleaningTarget` 阶段被安全卸载；若它已被设为 Active Scene，框架先尝试恢复旧 Active Scene。清理与 `Finally` 完成后，请求才进入终态（`Failed` 或 `Cancelled`）。

一个 `MSceneFlow` 同时只允许一条切换；忙碌时（`IsBusy == true`）再次调用 `Transition` 会明确抛出 `InvalidOperationException`。
