# MFramework (`com.mstudio.mframework`)

MFramework 是 MSTUDIO 面向独立游戏开发者制作的轻量运行时框架。源码使用统一的 `MFramework` 命名空间，配套中文使用手册和 API 参考。

## 安装

**Unity 6 项目请先安装兼容的依赖包**：在宿主项目的 Package Manager 中，先安装适用于当前编辑器版本的 Addressables 与 UGUI，再添加 MFramework。包中保留的最低依赖声明（Addressables 1.22.3、UGUI 1.0.0）面向团结及旧版 Unity；Addressables 1.22.3 使用的旧对象标识 API 已在较新的 Unity 6 中移除，不能直接用于 Unity 6.6。

在 Unity 或团结引擎的 Package Manager 中选择 **Add package from git URL**，输入：

```text
https://github.com/MSTUDIOHUB/MFramework.git#v0.1.0
```

也可以把下面一项加入项目 `Packages/manifest.json` 的现有 `dependencies` 对象：

```json
"com.mstudio.mframework": "https://github.com/MSTUDIOHUB/MFramework.git#v0.1.0"
```

仓库根目录就是 UPM 包，不需要添加 `?path=`。安装 Git 包需要本机 Git 可用。若需要修改框架源码，可将仓库克隆到宿主项目的 `Packages/com.mstudio.mframework`，作为嵌入式包使用。

## 引擎与依赖

- 包声明的最低引擎版本：2022.3；源码以该版本的 API 范围为设计目标。
- 开发项目：团结引擎 1.10.4（Editor 2022.3.62t16）。原版 Unity 2022.3 的导入与运行结果需要单独验证。
- 包内最低依赖声明：`com.unity.addressables` 1.22.3、`com.unity.ugui` 1.0.0。使用更新编辑器时，由宿主项目显式选择与编辑器兼容的依赖版本。
- Unity 6 实测环境：Unity 6000.6.5f1、Addressables 2.11.2、UGUI 2.6.0、Unity Test Framework 1.8.0；150 项 EditMode 与 9 项 PlayMode 测试全部通过，115 个资产导入后的 GUID 与仓库一致。
- 运行时程序集：`MStudio.MFramework.Runtime`。
- 无引擎基础程序集：`MStudio.MFramework.Foundation`，包含存档核心与可恢复随机源，并提供 `netstandard2.1` 独立构建工程。

## 模块与使用方式

| 模块 | 用途 |
| --- | --- |
| Core / Event / Action / StateMachine | 显式服务注册、模块生命周期、类型安全事件、动作编排与状态切换 |
| Addressables / Pool / SceneFlow | 资源所有权、对象复用、分阶段的 Additive 内容场景切换 |
| UI / Audio | UGUI 界面导航、BGM 与 SFX 管理 |
| Config / Extensions / Platform | 配置及引用校验、常用扩展、可替换平台桥接 |
| Random / Save | 可恢复的 PCG 随机流、带校验与版本迁移的完整快照存档 |

业务脚本通过 `using MFramework;` 使用公开运行时 API。在常驻 Bootstrap 场景中以 `MGameEntry` 创建 `MFrameworkApp`，按需注册模块。完整接入代码见 [5 分钟快速上手](Documentation~/index.md)。

框架采用显式注册与明确的资源释放入口；事件、动作等常规更新路径复用缓冲以减少分配。嵌套发布、缓冲扩容及异常路径仍可能产生分配，具体约束见各模块手册。

## 文档

- [文档首页](Documentation~/index.md)：架构总览与快速上手
- [使用手册](Documentation~/manual/index.md)：14 个模块的接入方法与使用边界
- [API 参考](Documentation~/api/index.md)：公开类型与成员索引
- [文档构建说明](Documentation~/manual/build-docfx.md)：构站与更新 API 参考

文档 Markdown 源码、API 参考快照和 DocFX 配置随仓库提供。在安装 .NET SDK 8 或更高版本后，从仓库根目录执行：

```bash
bash Documentation~/build.sh
```

默认使用随附的 API 参考快照生成站点，无需安装引擎。添加 `--serve` 可通过本地 HTTP 服务预览。输出位于 `Documentation~/_site/`，可自行部署到网站子路径。生成站点与构建缓存不提交 Git；发布仓库保留可重建站点的源码和配置。

更新 API 参考还需要 Python 3.8 或更高版本，以及引用当前源码的宿主引擎所生成的 Runtime/Foundation C# 工程：

```bash
bash Documentation~/build.sh --update-api "/path/to/UnityProject"
```

具体准备步骤见[文档构建说明](Documentation~/manual/build-docfx.md)。

## 测试

在宿主 Unity/团结项目中安装与该编辑器版本兼容的 **Unity Test Framework**。然后在项目 `Packages/manifest.json` 的顶层 `testables` 数组中加入包名；已有数组时保留原有项目：

```json
{
  "testables": [
    "com.mstudio.mframework"
  ]
}
```

等待脚本编译完成后，打开 **Window > General > Test Runner**，分别运行本包的 **EditMode** 与 **PlayMode** 测试。测试源码位于 `Tests/`，不会作为普通运行时程序集引入游戏。

仅验证无引擎基础层编译时，可在仓库根目录执行：

```bash
dotnet build Tools~/MStudio.MFramework.Foundation.csproj
```

该构建不代替引擎测试或目标平台的运行验证。

## 许可证与反馈

MFramework 采用 [Apache License 2.0](LICENSE.md)。PCG 等第三方来源与许可证见 [Third-Party Notices](Third%20Party%20Notices.md)。问题反馈与改进建议请提交至 [GitHub Issues](https://github.com/MSTUDIOHUB/MFramework/issues)。
