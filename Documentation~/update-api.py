#!/usr/bin/env python3
"""从显式指定的 Unity 工程更新 API 快照，保留手写导航和文档。"""

import argparse
import json
import re
import subprocess
import tempfile
from pathlib import Path


def run(*args, cwd):
    subprocess.run(args, cwd=cwd, check=True)


def portable_source_paths(text, package_root):
    """仅归一化 API 的 source.path，不改成员说明或示例。"""
    pattern = re.compile(r"^(\s+path: )(.+)$", re.MULTILINE)

    def replace(match):
        value = match.group(2).strip().strip("\"'").replace("\\", "/")
        if not (value.startswith("/") or re.match(r"^[A-Za-z]:/", value)):
            return match.group(0)
        # Unity 工程可能通过 embedded、local 或 Git 包引用同一份源码。
        # Runtime 是发布包内的稳定路径，并额外检查该文件确实存在。
        marker = "/Runtime/"
        if marker not in value:
            raise ValueError(f"发现不属于包运行时的绝对源码路径：{value}")
        relative = "Runtime/" + value.rsplit(marker, 1)[1]
        if not (package_root / relative).is_file():
            raise ValueError(f"API 源码与当前包不一致：{relative}")
        return match.group(1) + relative

    return pattern.sub(replace, text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("unity_project", type=Path, help="已生成 C# 工程的 Unity/团结项目目录")
    args = parser.parse_args()
    project = args.unity_project.resolve()
    docs = Path(__file__).resolve().parent
    package = docs.parent
    projects = ["MStudio.MFramework.Foundation.csproj", "MStudio.MFramework.Runtime.csproj"]
    for name in projects:
        if not (project / name).is_file():
            parser.error(f"缺少 {project / name}；请先在编辑器中重新生成 C# 工程。")

    for name in projects:
        run("dotnet", "restore", str(project / name), cwd=project)

    # 在临时目录完成提取和路径校验，成功后才替换已有生成文件。
    # DocFX 自动生成的扁平 toc.yml 和缓存不进入发布快照。
    with tempfile.TemporaryDirectory(prefix=".api-build-", dir=docs) as temp_name:
        temp = Path(temp_name)
        generated = temp / "api"
        config = {
            "metadata": [{
                "src": [{"src": str(project), "files": projects}],
                "dest": str(generated),
                "properties": {"Configuration": "Debug"},
                "disableGitFeatures": True,
            }]
        }
        config_path = temp / "docfx.json"
        config_path.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        run("dotnet", "tool", "run", "docfx", "--", "metadata", str(config_path), "--logLevel", "verbose", cwd=docs)

        expected = ["MFramework.yml", "MFramework.MFrameworkApp.yml", "MFramework.MRandom.yml", "MFramework.MSave-1.yml"]
        for name in expected:
            if not (generated / name).is_file():
                raise RuntimeError(f"API 提取不完整，未替换已有快照：缺少 {name}")

        snapshots = {}
        for path in generated.glob("*.yml"):
            if path.name != "toc.yml":
                snapshots[path.name] = portable_source_paths(path.read_text(encoding="utf-8"), package)

        api = docs / "api"
        for name, text in snapshots.items():
            (api / name).write_text(text, encoding="utf-8")
        for path in api.glob("*.yml"):
            if path.name not in snapshots and path.name != "toc.yml":
                path.unlink()
        (api / ".manifest").unlink(missing_ok=True)
        print(f"API 快照已更新：{len(snapshots)} 个文件；保留 api/index.md 和分组 api/toc.yml。")


if __name__ == "__main__":
    main()
