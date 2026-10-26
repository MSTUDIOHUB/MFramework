---
title: Event 模块
---

# Event：发布事实，减少硬引用

## 从项目问题开始

击杀敌人后，金币、任务、音效和 UI 都可能响应。如果敌人脚本逐一持有这些系统，依赖会不断扩散。事件总线适合发布“已经发生的事实”，让接收方自行决定是否处理。

## 当前 API

- `IMEventBus`：`Subscribe<TEvent>`、`Publish<TEvent>` 与 `Clear`。
- `MEventBus`：同步、类型安全、限定创建线程使用的实现。
- `MEventDispatchException`：一个或多个订阅回调失败时汇总异常。

## 最小示例

```csharp
using MFramework;

public readonly struct EnemyDefeated
{
    public EnemyDefeated(int reward) => Reward = reward;
    public int Reward { get; }
}

public sealed class RewardPresenter
{
    private readonly MDisposableBag _lifetime = new MDisposableBag();

    public RewardPresenter(IMEventBus events)
    {
        events.Subscribe<EnemyDefeated>(OnEnemyDefeated).AddTo(_lifetime);
    }

    private void OnEnemyDefeated(EnemyDefeated message)
    {
        // 刷新金币显示。
    }
}
```

发布端只需要：

```csharp
// 先完成 Subscribe 并保存返回的句柄；Publish 会在当前线程同步通知同类型订阅者。
events.Publish(new EnemyDefeated(20));
```

## 重要语义

- 发布是同步调用，不会自动切换线程，也不是异步队列。
- 事件类型使用精确的泛型类型匹配。
- 发布时修改订阅列表是安全的，因为内部遍历快照。
- 传入 Unity `owner` 后，owner 被销毁的订阅会失效；仍建议保存并释放返回的句柄。
- 某个回调抛异常时，其他快照订阅仍会执行，最后统一抛出 `MEventDispatchException`。

## 不适合事件的情况

“请计算伤害并返回结果”是请求，不是事实；应直接调用明确接口。稳定的父子关系也不必为了“解耦”改成事件，否则调用链会更难追踪。
