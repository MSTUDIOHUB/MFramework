---
title: Addressables 模块
---

# Addressables：让加载与释放配对

## 从项目问题开始

`Addressables.LoadAssetAsync` 本身已经能加载资源，真正容易出错的是“谁持有 Handle、失败怎样报告、页面关闭后晚到的回调怎么办、何时 Release”。MFramework 不重写 Catalog、Bundle 或引用计数，只把这些生命周期显式化。

## 三层对象

1. `MAddressables` 创建加载、实例化、场景或依赖下载请求。
2. Request 暴露 `State`、`Progress`、`Error`、`Completed`，成功后提供 Lease。
3. `MAssetScope` 可以集中持有多个请求，并按逆序释放（支持 `scope.Own(request)` 或链式 `request.AddTo(scope)`）。

## 加载资产

```csharp
// 字段保存页面级 Scope：它拥有本页面发起的请求，页面销毁时统一归还。
private MAssetScope _scope;

void LoadPortrait()
{
    // 先创建所有权容器，再用 AddTo（或传参）把新请求登记进去；后续无需逐个记住并释放请求。
    _scope = new MAssetScope();
    MAssetLoadRequest<Sprite> request =
        MAddressables.LoadAssetAsync<Sprite>("ui.portrait.archer").AddTo(_scope);

    request.Completed += completed =>
    {
        // 回调必须区分成功与失败：只有成功请求才拥有可读取的 Lease 和 Asset。
        if (completed.State == MAddressablesRequestState.Succeeded)
        {
            portraitImage.sprite = completed.Lease.Asset;
        }
        else
        {
            Debug.LogError(completed.Error);
        }
    };
}

void OnDestroy()
{
    // Unity 生命周期结束时停止使用资源并释放 Scope；?. 允许尚未开始加载的页面安全退出。
    _scope?.Dispose();
}
```

也可以传入 Inspector 配置的 `AssetReference`。公开 API 保留原始 Key 与 AssetReference，因此仍能直接查阅 Addressables 官方手册。

## 请求与取消语义

- `Dispose` 一个 Pending 请求会把它标记为 `Abandoned`，清除业务回调。
- 这不是网络传输层取消；底层操作仍会完成，然后框架释放晚到的结果。
- 成功的资源由 `MAssetLease<TAsset>` 持有；实例与场景分别由 `MGameObjectInstanceLease`、`MSceneLease` 持有。
- Lease 的 `Dispose` 可重复调用，但业务仍应保证所有权只有一个清晰入口。
- `MSceneLease.Dispose()` 表示发起场景卸载且不等待；需要观察卸载完成状态或处理错误时，应调用 `UnloadAsync()`。
- 下载请求完成后会释放操作 Handle，它没有资产 Lease。

## 场景所有权与可等待卸载

场景加载成功后可以使用 `TakeLease()` 把 Lease 从请求转交给长期服务。转移后释放加载请求不会卸载该场景：

```csharp
// TakeLease 把已加载场景的释放责任从 Request 转给 scene，之后由 scene 发起卸载。
MSceneLease scene = completed.TakeLease();
MSceneUnloadRequest unload = scene.UnloadAsync();

unload.Completed += result =>
{
    // 必须显式处理卸载失败，Error 中保存了供日志与恢复流程使用的原因。
    if (result.State == MAddressablesRequestState.Failed)
    {
        Debug.LogError(result.Error);
    }
};
```

Addressables 不能直接卸载 `activateOnLoad: false` 且尚未激活的场景。`MSceneUnloadRequest` 会在必要时先完成激活，再卸载并等待物理完成；这意味着用于清理的目标仍可能短暂执行 `Awake/OnEnable`。

为避免无法安全回收的最后场景，`LoadSceneMode.Single + activateOnLoad: false` 会在启动 Addressables 前被拒绝。需要延迟激活、可等待卸载或 Scope 清理的内容场景应使用 Additive，并保留一个已经激活的 Bootstrap 场景。Single 更适合作为替换整个 Unity 场景栈的直接导航，不应交给 `MSceneFlow` 管理。

完整的内容场景切换、前后阶段 Hook、失败收口和串行 Busy 规则请使用 [SceneFlow](scene-flow.md)。

## 对象池注意事项

通过 `LoadAssetAsync<GameObject>` 得到 Prefab 后，可把它注册到 `MPool`。退出战斗时先停止使用并清空池，再释放 Prefab Lease：

```text
停止生成 -> 归还活动对象 -> MPool.Clear -> MAssetLease.Dispose
```

否则池仍可能持有已经被 Addressables 释放的 Prefab。
