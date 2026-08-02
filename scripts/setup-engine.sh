#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
engine_venv="$project_root/.venv-engine"

python3 -m venv "$engine_venv"
"$engine_venv/bin/python" -m pip install --upgrade pip
"$engine_venv/bin/python" -m pip install -r "$project_root/engine_host/requirements.txt"

echo "本地引擎依赖安装完成：$engine_venv"
