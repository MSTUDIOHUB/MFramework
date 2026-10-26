---
title: Extensions 模块
---

# Extensions：少量、明确、可发现

## 设计原则

Unity 自带 API 已经清楚时不重复套壳。扩展方法只保留高频、行为无歧义且能减少噪声的操作。

## 当前扩展

### Vector3 分量替换

```csharp
// WithY 返回新的 Vector3，不会直接改写 transform.position。
Vector3 next = transform.position.WithY(0f);
```

`WithX`、`WithY`、`WithZ` 返回新值，不修改原 `Vector3`。

### 获取或添加组件

```csharp
// 已存在则复用组件，否则添加；对象为 null 或已销毁时会抛出明确异常。
CanvasGroup group = gameObject.GetOrAddComponent<CanvasGroup>();
```

目标 GameObject 为 null 或已经被销毁时会抛出明确异常。

### RectTransform 单轴位置

```csharp
// 每次只替换 anchoredPosition 的一个轴，另一个轴保持不变。
panel.SetAnchoredPositionX(120f);
panel.SetAnchoredPositionY(0f);
```

每次只替换一个 `anchoredPosition` 分量。

### Disposable 与 AssetScope 所有权

```csharp
// AddTo 把订阅句柄或资源请求的释放责任交给对应的生命周期容器，并返回原实例。
events.Subscribe<GameStarted>(OnGameStarted).AddTo(_lifetimeBag);
MAddressables.LoadAssetAsync<Sprite>("ui.portrait.archer").AddTo(_assetScope);
```

`AddTo` 同时支持 `MDisposableBag` 和 `MAssetScope`，把同一个可释放对象交给对应容器托管并返回原对象。

## 为什么没有 Vector3ToInt

“转为整数”没有说明向下取整、向上取整还是四舍五入，容易产生隐蔽错误。应直接使用 Unity 已提供的 `Vector3Int.FloorToInt`、`CeilToInt` 或 `RoundToInt`。

扩展命名若不能从名字准确推断行为，就不应加入框架。
