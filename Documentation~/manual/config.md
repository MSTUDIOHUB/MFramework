---
title: Config 模块
---

# Config：按类型和 ID 查询配置与校验引用

## 从项目问题开始

佣兵价格、敌人生命和波次奖励不应散落在脚本常量中。`MConfig` 保存显式注册的 `ScriptableObject` 配置，并提供类型安全查询与统一校验。

## 定义配置

```csharp
using MFramework;
using UnityEngine;

[CreateAssetMenu(menuName = "Demo/Mercenary Config")]
public sealed class MercenaryConfig : MConfigAsset
{
    // SerializeField 让私有字段可在 Unity Inspector 中填写，只读属性只负责对外读取。
    [SerializeField] private int cost;
    public int Cost => cost;

    public override void Validate(MConfigValidationContext context)
    {
        // 校验只报告问题，不在运行时偷偷改写配置资产。
        if (cost <= 0)
        {
            context.AddError("佣兵价格必须大于零。");
        }
    }
}
```

基类中的 ID 在 Inspector 中填写。加载资产后显式注册：

```csharp
// 先注册已经加载的资产；同一注册类型下的 ID 必须唯一。
MConfig configs = new MConfig();
configs.RegisterRange(mercenaryConfigs);

// Get 用“注册类型 + ID”查询；所有相关配置注册完成后再统一校验。
MercenaryConfig archer = configs.Get<MercenaryConfig>("archer");
var errors = configs.ValidateAll();
```

## 示例一：面板引用必填标题与可选说明

下面使用普通界面配置演示跨类型引用，不需要接入游戏规则。将各个公开的 `ScriptableObject` 类型放在各自同名脚本中，创建资产后在 Inspector 填写基类 ID。

```csharp
using MFramework;
using UnityEngine;

[CreateAssetMenu(menuName = "Example/Text Config")]
public sealed class TextConfig : MConfigAsset
{
    [SerializeField] private string text;
    public string Text => text;

    public override void Validate(MConfigValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            context.AddError(nameof(text), "显示文本不能为空。");
        }
    }
}

[CreateAssetMenu(menuName = "Example/Panel Config")]
public sealed class PanelConfig : MConfigAsset
{
    [SerializeField] private string titleId;
    [SerializeField] private string helpId;

    public override void Validate(MConfigValidationContext context)
    {
        context.RequireReference<TextConfig>(nameof(titleId), titleId);
        context.RequireReference<TextConfig>(nameof(helpId), helpId, optional: true);
    }
}
```

`titleId` 必须指向注册过的 `TextConfig`。`helpId` 允许 null、空字符串或仅有空白；一旦填写了非空 ID，就必须存在对应条目。`optional: true` 不会忽略拼错的 ID，也不会把不存在的条目当作可省略内容。

```csharp
MConfig configs = new MConfig();
// 可以先注册引用方；Register 不提前执行校验。
configs.RegisterRange(panelConfigs);
configs.RegisterRange(textConfigs);

// 等本批次全部类型注册完成，再检查所有引用与各配置自己的业务约束。
var errors = configs.ValidateAll();
foreach (MConfigValidationError error in errors)
{
    Debug.LogError(error.ToString());
}
```

假设来源 `PanelConfig` 的 ID 为 `settings`，`helpId` 填了不存在的 `missing-help`，错误包含：

```text
PanelConfig[settings].helpId: 未找到类型为 TextConfig、Id 为 'missing-help' 的目标配置（未注册或已被销毁）。
```

若配置类型有命名空间，引用错误的说明中会显示目标类型的完整名称。`ConfigType` 和 `ConfigId` 始终定位来源配置，`FieldPath` 为 `helpId`，`Message` 说明期望的目标类型与 ID。字段路径由配置显式提供，框架不会用路径反射访问字段。

## 示例二：配置集合引用面板，并定位具体条目

```csharp
using MFramework;
using UnityEngine;

[CreateAssetMenu(menuName = "Example/Panel Group Config")]
public sealed class PanelGroupConfig : MConfigAsset
{
    [SerializeField] private string[] panelIds;

    public override void Validate(MConfigValidationContext context)
    {
        if (panelIds == null || panelIds.Length == 0)
        {
            context.AddError(nameof(panelIds), "至少选择一个面板。");
            return;
        }

        for (int i = 0; i < panelIds.Length; i++)
        {
            context.RequireReference<PanelConfig>($"panelIds[{i}]", panelIds[i]);
        }
    }
}
```

需要取得目标、检查自定义跨配置规则时，在配置的 `Validate` 内使用 `TryResolve`：

```csharp
// 查找本身不添加错误；根据业务情况决定如何报告。
TextConfig title;
if (context.TryResolve<TextConfig>(titleId, out title)
    && !string.IsNullOrEmpty(title.Text) && title.Text.Length > 40)
{
    context.AddError(nameof(titleId), "标题文本超过此面板允许的长度。");
}
```

## 类型、错误与调用边界

- 键由注册时的泛型类型和 ID 共同构成，ID 使用 Ordinal 比较，不自动裁剪空格或改变大小写。
- 不同类型可以有相同 ID。例如 `TextConfig[settings]` 和 `PanelConfig[settings]` 可以同时存在；查询 `TextConfig` 不会误用同名面板。
- `RequireReference` 返回 true 表示引用存在或可选空值合法；返回 false 时已添加一条错误，不抛出缺失引用异常。
- `TryResolve` 是校验上下文中的查询，空白 ID 或已销毁目标返回 false 并输出真正的 null。
- `ValidateAll` 通过显式校验收集配置自身与引用错误，不扫描程序集、不推断字段、不修改资产。
- `MConfig` 不负责 Excel 导入、远程更新、玩家存档、版本迁移或资源加载。配置可由 [Addressables](addressables.md) 加载，再注册到 `MConfig`。
