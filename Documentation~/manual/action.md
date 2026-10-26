---
title: Action 模块
---

# Action：组合短时流程

## 从项目问题开始

使用多个协程完成“等待、播放、并行、重复”时，流程容易分散在多个协程和布尔变量中，取消、异常和对象销毁后的行为也很难统一。Action 模块把流程表示为可组合、单次使用的节点。

## 当前节点

`MAction` 可以创建：

- `Callback`：立即调用一次；
- `Delay`：等待指定秒数；
- `WaitUntil`：等待条件成立；
- `Sequence`：依次执行子节点；
- `Parallel`：同时执行并等待所有子节点；
- `Repeat`：通过工厂重复创建新节点；
- `Custom`：由调用方提供开始、Tick 和取消逻辑。

`MActionRunner` 负责驱动根节点，`MActionHandle` 用于查看状态或取消。

## 最小示例

```csharp
// 先取得已注册的 Runner；Sequence 中的 lambda 到对应节点运行时才真正调用。
MActionRunner runner = app.Get<MActionRunner>();

MActionHandle handle = runner.Run(
    MAction.Sequence(
        MAction.Callback(() => ShowWaveTitle()),
        MAction.Delay(1.5f),
        MAction.Callback(() => StartWave())),
    owner: this,
    timeMode: MTimeMode.Unscaled);
```

把句柄加入页面或对象的 `MDisposableBag`，销毁时即可取消：

```csharp
// 把句柄交给 _lifetime 持有；Bag 释放时会取消尚未完成的动作。
handle.AddTo(_lifetime);
```

## 重要语义

- 每个 `IMActionNode` 只能启动一次；需要重复时必须通过工厂创建新节点。
- `Scaled` 跟随 `Time.timeScale`，`Unscaled` 可在暂停菜单中继续。
- owner 被销毁后，Runner 会在下一次 Tick 取消动作。
- 回调异常会把节点置为 `Failed`，并可从 `Failure` 查看原因。
- `Dispose` 句柄等价于取消，不表示“等待动作完成”。

## 使用边界

Action 适合 UI 动画编排、波次提示、冷却等待等短时流程。长期互斥的角色行为更适合 [FSM](state-machine.md)；资源加载仍由 Addressables 请求表达，不要用 Action 隐藏资源所有权。
