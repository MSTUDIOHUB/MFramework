---
title: Core 模块
---

# Core：入口、注册与生命周期

## 从项目问题开始

Unity 项目很容易出现多个 `MonoBehaviour` 单例各自在 `Awake` 中初始化：谁先创建、谁先更新、退出时谁先释放都不清楚。Core 模块把这些顺序集中到一个显式入口。

## 当前职责

- `MFrameworkApp`：按注册类型保存服务（提供 `Register`、`Has`、`Get`、`TryGet`）；按注册顺序初始化和 Tick，按相反顺序关闭模块。
- `MGameEntry`：把 Unity 的 `Awake/Update/FixedUpdate/LateUpdate/OnDestroy` 转换成框架生命周期。
- `IMFrameworkModule`：声明 `Initialize` 与 `Shutdown`。
- `IMFrameworkTickable`、`IMFrameworkFixedTickable`、`IMFrameworkLateTickable`：声明三种更新时机。
- `MFrameTime`：同时传递缩放和未缩放帧时间。
- `MDisposableBag`：按后进先出的顺序集中释放 `IDisposable`。

Core 不扫描程序集、不自动寻找场景组件，也不会用反射创建服务。注册关系必须在入口中读得出来。

## 最小入口

```csharp
using MFramework;

public sealed class GameEntry : MGameEntry
{
    protected override void Configure(MFrameworkApp app)
    {
        // 按接口注册后，后续必须用 IMEventBus 作为 Get 的键；注册顺序也决定模块初始化顺序。
        app.Register<IMEventBus>(new MEventBus());
        app.Register(new MActionRunner());
        app.Register(new MConfig());
    }
}
```

`Register<TService>` 中的泛型类型就是之后 `Get<TService>`、`TryGet<TService>` 和 `Has<TService>` 使用的键。若按接口注册 `IMEventBus`，就应按同一个接口查询。

## 生命周期规则

1. 只能在 `Created` 状态注册服务。
2. `Initialize` 只调用一次；初始化中途失败时，已成功初始化的模块会逆序回滚。
3. 只有实现对应 Tick 接口的服务才会收到更新。
4. `Shutdown` 逆序关闭已经初始化的 `IMFrameworkModule`。
5. 普通 `IDisposable` 服务不会因为被注册就自动释放；应由模块、场景入口或 `MDisposableBag` 明确持有。

## 何时直接使用 Unity API

场景中只需要一次的 `Time.deltaTime`、`Destroy` 或组件生命周期不需要再套一层。Core 只解决跨模块的统一顺序和服务访问，不替代 Unity 的对象模型。
