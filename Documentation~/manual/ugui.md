---
title: UGUI 模块
---

# UGUI：分层、导航与统一按钮语义

## 从项目问题开始

多级菜单如果由每个按钮自行 `SetActive`，很快会出现返回路径不一致、弹窗与页面抢占输入，以及“取消”和“上一步”各写一套逻辑。当前 UI 模块只面向 UGUI，把视图生命周期、页面历史、弹窗顺序和通用操作集中管理。

## 当前结构

- `MUIRoot`：在场景中引用 Page、Popup、Overlay 三个 `RectTransform` 层。
- `MUIService`：显式注册视图 Prefab（可通过 `IsRegistered<TView>()` 查询），并按具体类型缓存一个实例。
- `MUIRouter`：管理页面栈、弹窗栈、覆盖层和统一返回。
- `MUIView`：页面、弹窗、覆盖层的共同生命周期基类。
- `MUIPage`、`MUIPopup`、`MUIOverlay`：决定实例所在层级和导航语义。
- `MUIActionButton`：把 UGUI `Button` 点击转换为 Back、Cancel、Confirm、Previous 或 Next。

## 初始化

```csharp
using MFramework;
using UnityEngine;

public sealed class GameEntry : MGameEntry
{
    [SerializeField] private MUIRoot uiRoot;
    [SerializeField] private MainMenuPage mainMenuPrefab;
    [SerializeField] private HowToPlayPage howToPlayPrefab;
    [SerializeField] private SettingsPopup settingsPrefab;

    protected override void Configure(MFrameworkApp app)
    {
        // 先由 Root 创建 Service 并注册全部 Prefab，再用同一个 Service 构造 Router。
        MUIService ui = uiRoot.CreateService()
            .Register(mainMenuPrefab)
            .Register(howToPlayPrefab)
            .Register(settingsPrefab);

        MUIRouter router = new MUIRouter(ui);
        // 按 Service、Router 的顺序注册，App 初始化时会保持这个依赖顺序。
        app.Register(ui);
        app.Register(router);
    }
}
```

可以先创建 Router 再显示视图；Router 既会监听后续的 `ViewCreated`，也会补绑 Service 已经缓存的视图。启动代码仍建议按“创建 Service、创建 Router、显示首页”的顺序书写，阅读最直观。

## 页面操作

```csharp
// 先取得已注册的 Router；下面的调用会依次修改同一份页面历史栈。
MUIRouter router = app.Get<MUIRouter>();

router.Push<MainMenuPage>();
router.Push<HowToPlayPage>(new { Step = 1 });
router.Back();
router.Replace<BattlePage>();
router.PopToRoot();
```

Push 已在历史中的缓存页面时，会关闭它上方的页面并回到该位置。Replace 只替换当前页；如果目标类型已在更早历史中，会移除旧位置再把同一缓存实例放到栈顶。

## 弹窗与覆盖层

```csharp
// Popup 使用独立栈；Overlay 只改变覆盖层显示，不会修改页面历史。
router.ShowPopup<SettingsPopup>();
router.CloseTopPopup(MUIPopupResult.Confirm());

router.ShowOverlay<LoadingOverlay>();
router.HideOverlay<LoadingOverlay>();
```

弹窗拥有独立栈；覆盖层不修改页面或弹窗历史。`MUIPopup.OnClosed` 可以接收确认状态和可选业务值。

## 统一语义操作

在 UGUI Button 上添加 `MUIActionButton`，选择 Back、Cancel、Confirm、Previous 或 Next。视图通过 `OnAction` 返回路由指令：

```csharp
public sealed class HowToPlayPage : MUIPage
{
    protected override MUIActionResult OnAction(MUIAction action)
    {
        if (action == MUIAction.Next)
        {
            return MUIActionResult.NavigateTo<BattlePage>();
        }

        if (action == MUIAction.Previous)
        {
            return MUIActionResult.Handled(); // 在页面内部切换到上一个教学步骤
        }

        if (action == MUIAction.Back)
        {
            return MUIActionResult.Back(); // 离开当前页面，返回页面历史
        }

        return MUIActionResult.Unhandled();
    }
}
```

还可返回 Handled、ClosePopup 或 Rejected。Rejected 会触发 Router 的 `ActionRejected` 事件，可由业务层显示 Toast 或行内错误。Cancel 表示撤销临时修改，不等同于离开页面；页面未处理 Cancel 时不会自动返回。

## 统一返回顺序

`RequestBack()` 返回 `bool`；视图消费操作或真正完成返回时为 true，到达根页面时为 false。`CanGoBack` 表示当前是否存在可关闭的弹窗或上一页。调用统一返回时：

1. 有弹窗时，先向顶层弹窗发送 `MUIAction.Back`；
2. 否则向当前页面发送 `MUIAction.Back`；
3. 若视图没有自定义处理（返回 `Unhandled`），弹窗默认对 `Cancel`、`Back` 或 `Previous` 以取消结果关闭，页面默认对 `Back` 或 `Previous` 返回上一层；
4. 页面已经在根部时触发 `RootBackRequested`，由游戏决定暂停、退出确认或平台返回。

## 当前边界

- 只支持 UGUI，不支持 UI Toolkit。
- Prefab 必须显式注册；当前服务不负责 Addressables 加载。
- 每种视图只缓存一个实例，因此“同类型页面再次 Push”代表回到已有缓存页，而不是创建一条同类型路由快照；它也不适合同时显示多个同类型业务弹窗。
- `Arguments` 是缓存视图最近一次成功显示时保存的 `object`，不是不可变的历史快照；Popup Value 也使用 `object`。业务层应在视图入口集中校验类型。
- 页面与弹窗只有处于对应栈顶时才能驱动导航；隐藏视图和被上层弹窗遮挡的页面动作会被忽略。
- `MUIService.Show/Hide` 是不修改路由栈的底层能力；Page 与 Popup 的正常业务导航应使用 Router，避免可见状态与历史分离。
- 视图创建、显示、隐藏与关闭钩子抛出异常时，框架会尽量恢复原页面或完成安全隐藏；生命周期钩子中同步发起的重入导航会被阻止。
- `MUIService.Dispose` 会销毁缓存实例并清空注册；视图应在 `OnHide` 或 `OnDispose` 清理订阅、Action 与资源 Scope。
