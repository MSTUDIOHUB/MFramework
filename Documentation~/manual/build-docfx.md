---
title: 生成 DocFX 手册
---

# 生成 DocFX 手册

## 文档源码与网站文件

`Documentation~` 保存公开使用手册、API YAML 快照以及构建配置。它们随框架仓库维护，方便查阅和修改。

DocFX 将这些内容生成为 `_site` 中的 HTML、搜索索引、样式和脚本。`_site` 是可重建的输出，不需要提交到 GitHub，也不需要随 UPM 包分发。托管网站时部署这份输出即可，网站服务器不需要安装 Unity 或 .NET。

## 生成网站

前置条件为 .NET SDK 8 或更高版本，以及 Bash（Windows 可使用 Git Bash）。DocFX 版本由本目录的 `.config/dotnet-tools.json` 锁定为 `2.78.5`，脚本会自动恢复该工具。

在独立框架仓库根目录执行：

```bash
bash Documentation~/build.sh
```

默认构建直接读取仓库中的 API YAML 快照，不需要安装 Unity，不依赖宿主项目的 `.csproj`，也不会重新提取或删除 API 快照。

生成结果位于 `Documentation~/_site/`。每次构建会清理该输出目录，避免已经删除的文档在旧网站中残留。构建启用 `--warningsAsErrors`，请处理断链或未解析的 API 引用后再部署。

本地预览：

```bash
bash Documentation~/build.sh --serve
```

访问终端显示的 HTTP 地址。搜索依赖站点索引和脚本，应通过 HTTP 预览，而不是直接双击 HTML 文件。

如果包嵌入在 Unity/团结项目的 `Packages/com.mstudio.mframework` 中，从项目根目录执行以下命令也有效：

```bash
bash Packages/com.mstudio.mframework/Documentation~/build.sh --serve
```

不使用 Bash 时，可以在 `Documentation~` 目录执行下列命令；重新生成前请自行清理旧的 `_site` 输出：

```bash
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet tool run docfx -- build docfx.json --warningsAsErrors --serve
```

## 修改源码后更新 API

API YAML 是从源码 XML 注释提取的快照。维护框架并修改公开 API 或注释后，需要显式更新快照，再生成网站。

更新 API 另外需要 Python 3.8 或更高版本，以及一个引用**当前这份框架源码**的 Unity/团结工程：

1. 让编辑器完成包和脚本导入，处理 Console 中的编译错误。
2. 在编辑器的外部工具设置中启用所用包来源的 C# 工程生成，然后重新生成工程文件。
3. 确认宿主项目根目录存在 `MStudio.MFramework.Foundation.csproj` 和 `MStudio.MFramework.Runtime.csproj`。
4. 在框架仓库根目录运行，并将路径替换成实际的宿主项目目录：

```bash
bash Documentation~/build.sh --update-api "/path/to/MyUnityProject"
```

脚本会先恢复两个工程的依赖，在临时目录中完成 API 提取，校验基础页和源码路径后才替换 `api/*.yml`。它保留手写的 `api/index.md` 及按 14 个模块组织的 `api/toc.yml`，并删除已失效的生成页。输出中的源码路径转换成包内相对路径，临时配置和 DocFX 缓存不会进入 API 快照。

这些 `.csproj` 由编辑器生成，应通过编辑器更新，不要手工维护或提交到框架仓库。DocFX 使用 MSBuildWorkspace 和 Roslyn 读取工程、依赖与 XML 注释，因此 API 提取需要对应编辑器及其依赖可用；单纯生成网站不需要这些条件。

## 放到自己的网站

可以把整个 `_site` 的内容部署到网站的独立子路径，例如 `https://example.com/mframework/`，然后从网站导航链接过去。默认模板使用相对路径，API 页、模块导航、样式和搜索资源可以随整个目录一起移动；保留内部目录结构和文件名即可，无需使用 GitHub Pages。

这是一组相互链接的静态页面，包含搜索索引与脚本，不是单个 HTML 文件。如果网站使用 WordPress 或其他 CMS，可在同域名子路径托管这组文件，或用单独的文档子域名。只有网站确实需要嵌入时再考虑 iframe。

部署时应让服务器直接提供该目录的静态文件和 `index.html`，不要将 API、JSON 或脚本请求重写成主网站的单页应用入口。用实际部署子路径检查一次 API 导航和搜索，并在更新后使缓存失效。

## 单独验证基础层

在框架仓库根目录执行：

```bash
dotnet build Tools~/MStudio.MFramework.Foundation.csproj
```

该工程目标为 `netstandard2.1`，语言版本为 C# 9，不依赖 Unity、Addressables 或 UGUI；开启 XML 文档生成，并将编译警告视为错误。它用于验证无引擎基础层编译，不代替 Unity EditMode、PlayMode 或目标平台测试。`Tools~` 中的 `bin`、`obj` 是生成物，无需随源码分发。

## 常见问题

### 更新 API 时找不到 csproj

先确认包内的 Runtime 和 Foundation asmdef 已成功导入，再让编辑器重新生成 C# 工程。外部工具设置需要包含当前使用的 embedded、local 或 Git 包来源。普通的默认构站不需要这些工程。

### metadata 阶段出现 MSBuild 错误

确认宿主 `.csproj` 引用了当前框架源码，编辑器和依赖包的路径仍然有效。更新脚本会输出详细日志，可以先在宿主项目中运行 `dotnet restore MStudio.MFramework.Runtime.csproj` 排查缺失引用。应修复包依赖、asmdef 或编辑器的工程生成设置。

### API 说明没有更新

确认成员有 `/// <summary>` 等 XML 注释，宿主项目已经重新生成 C# 工程，再使用 `--update-api`。默认构站只读取已保存的快照。

### metadata 提示工程引用没有匹配的元数据引用

DocFX 2.78.5 读取某些编辑器生成的 Runtime → Foundation 工程引用时，可能报告 `Found project reference without a matching metadata reference`。相同现象见 [DocFX 的 Unity 工程问题记录](https://github.com/dotnet/docfx/issues/9804)。应检查两个程序集的 API、中文继承注释及交叉链接是否完整；若缺少页面或类型说明，仍需修复提取问题。
