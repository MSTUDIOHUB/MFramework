#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
unity_project=""
serve=false

usage() {
  cat <<'USAGE'
用法：bash Documentation~/build.sh [--update-api <Unity 工程目录>] [--serve]

默认使用仓库内的 API YAML 快照生成静态网站，只需 .NET SDK。
--update-api  先从指定 Unity/团结工程重新提取 API；另需 Python 3.8 或更高版本。
--serve       构建后启动本地 HTTP 预览。
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --update-api)
      if [[ $# -lt 2 || -z "$2" ]]; then
        usage >&2
        exit 2
      fi
      unity_project="$2"
      shift 2
      ;;
    --serve)
      serve=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "未知参数：$1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ ! -f "${script_dir}/docfx.json" || ! -f "${script_dir}/../package.json" || ! -d "${script_dir}/api" ]]; then
  echo "无法确认 MFramework 文档目录：${script_dir}" >&2
  exit 1
fi

if [[ -n "${unity_project}" ]]; then
  command -v python3 >/dev/null || { echo "更新 API 需要 Python 3。" >&2; exit 1; }
  # 先解析调用者提供的路径，再切换工作目录；允许从任意目录调用脚本。
  unity_project="$(cd "${unity_project}" && pwd)"
fi

cd "${script_dir}"
dotnet tool restore --tool-manifest .config/dotnet-tools.json

if [[ -n "${unity_project}" ]]; then
  python3 update-api.py "${unity_project}"
fi

# 只清理固定的站点输出目录；API 快照和手写分组导航不会被构站删除。
if [[ -L "${script_dir}/_site" ]]; then
  echo "拒绝清理作为符号链接的 _site 目录。" >&2
  exit 1
fi
if [[ -d "${script_dir}/_site" ]]; then
  find "${script_dir}/_site" -mindepth 1 -delete
fi

build_args=(build docfx.json --warningsAsErrors)
if [[ "${serve}" == true ]]; then
  build_args+=(--serve --hostname 127.0.0.1)
fi
dotnet tool run docfx -- "${build_args[@]}"

echo "DocFX 网站已生成：${script_dir}/_site/index.html"
