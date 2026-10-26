---
title: FSM 模块
---

# FSM：显式表达互斥状态

## 从项目问题开始

敌人可能处于移动、攻击、受击或死亡状态。如果这些行为靠多个布尔值组合，就会出现“死亡但仍在攻击”等非法情况。有限状态机让同一时刻只有一个状态负责更新。

## 当前 API

- `IMState<TContext>`：定义 `Enter`、`Tick`、`Exit`。
- `MStateMachine<TContext>`：显式注册实例，并按泛型类型切换。

## 最小示例

```csharp
public sealed class EnemyContext
{
    public float DistanceToTarget;
}

public sealed class MoveState : IMState<EnemyContext>
{
    public void Enter(EnemyContext context) { }
    public void Tick(EnemyContext context, float deltaTime) { }
    public void Exit(EnemyContext context) { }
}

// 先创建共享上下文，再注册状态实例；未注册的状态不能切换。
EnemyContext context = new EnemyContext();
MStateMachine<EnemyContext> fsm = new MStateMachine<EnemyContext>(context)
    .Register(new MoveState());

// 切换完成后每帧传入 Unity 的 deltaTime，只推进当前状态。
fsm.ChangeState<MoveState>();
fsm.Tick(Time.deltaTime);
```

## 反射说明

实现内部会用 `typeof(TState)` 得到 `Type`，并把它作为字典键。这不是“扫描程序集并自动发现状态”：

- 没有 `Assembly.GetTypes`；
- 没有 `Activator.CreateInstance`；
- 没有按字符串调用方法；
- 所有状态对象都由业务代码明确创建和注册。

因此状态依赖、构造过程和 IL2CPP 保留关系仍然清晰。

## 状态切换规则

- 切到当前状态会返回 `false`，不会重复 Exit/Enter。
- 在 `Enter` 或 `Exit` 内递归切换会抛异常；把下一次切换留到后续 Tick。
- `Stop` 会退出当前状态并让状态机暂时无活动状态。
- `Dispose` 等价于 `Stop`，不会销毁注册的状态对象。
