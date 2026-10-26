---
title: Platform 模块
---

# Platform：隔离宿主 SDK

## 从项目问题开始

小游戏 SDK 的初始化、振动和本地存储 API 只在特定平台存在。业务脚本若直接引用 SDK 类型，普通 Unity 编辑器和未安装插件的项目就可能无法编译。

## 当前结构

- `IMPlatformBridge`：第一版 Demo 所需的最小能力集合。
- `MPlatformService`：业务侧稳定入口，并合并同时发起的初始化请求。
- `MEditorPlatformBridge`：编辑器安全回退；使用 `PlayerPrefs` 存储字符串，振动为空操作。
- `MPlatformInitializationResult`：明确表达成功或失败原因。

## 编辑器最小示例

```csharp
// Editor Bridge 是安全回退；业务只依赖 Service，不直接引用目标平台 SDK。
MPlatformService platform = new MPlatformService(new MEditorPlatformBridge());

platform.Initialize(result =>
{
    // 初始化失败立即返回，避免继续执行依赖平台就绪的存储操作。
    if (!result.Succeeded)
    {
        Debug.LogError(result.ErrorMessage);
        return;
    }

    string playerName = platform.LoadString("player.name", "新玩家");
});
```

## 适配规则

- MiniHost SDK 类型只应出现在专用适配程序集，不进入核心 Runtime。
- 游戏逻辑依赖 `IMPlatformBridge` 或 `MPlatformService`，不通过条件编译散落平台判断。
- 不支持的轻量能力可以安全降级，例如编辑器中的振动空实现。
- 平台桥接未就绪或抛出异常时，短震动与写入会安全跳过，读取会返回 fallback，并写入警告日志；核心玩法不会因为可选平台能力中断。
- 初始化失败可以再次调用 `Initialize`；同时等待的调用者都会收到同一轮结果。
- 每轮初始化都有独立编号，旧批次迟到或重复的 SDK 回调不会错误完成新批次；Service 关闭后也会忽略迟到结果。

当前核心包没有 MiniHost 具体 Bridge，不应把 `MEditorPlatformBridge` 描述成真机实现。
