# 屏译 PingYi

屏译是一个面向 Windows 10/11 与 Ubuntu X11 的开源截图 OCR/翻译工具。按 `Ctrl+Alt+D` 框选屏幕区域后，应用会显示可复制的原文和译文。

## 已实现

- Windows 虚拟桌面捕获、全局快捷键和多显示器框选；Ubuntu X11 提供对应的 Xlib 实现。
- 本地 OCR 已改为 C# + ONNX Runtime 直接推理，只携带 PaddleOCR 中英移动模型；离线翻译运行在独立 Argos 引擎进程中。
- 百度含位置 OCR、百度通用翻译、自定义 Chat Completions 兼容翻译接口。
- 提供商选择立即保存并应用；百度凭据可分别查看保存状态并进行真实验证。
- 本机大模型提供 llama.cpp、Ollama、LM Studio、vLLM 与通用 OpenAI 兼容预设；服务不可用时自动回退到本地 Argos 基础翻译。
- 原文/译文复制、重新处理、结果卡固定、托盘常驻和浅深色主题。
- 新版非对称主界面只保留截图主操作、当前方案、离线模型、隐私与实时状态；详细提供商、模型、本机大模型和凭据配置统一进入独立“设置”窗口。
- 模型缺失、服务断开或凭据不可用时，首页会动态展开带修复入口的提示卡；经典完整界面保留在“设置 → 外观”中，并可设为启动界面。
- 新程序图标已接入 Windows 可执行文件、系统托盘与 Linux 桌面包。
- Windows DPAPI、Linux Secret Service 凭据存储；默认不保存截图、原文或译文。
- 已保存的凭据在设置中以首尾字符加星号显示，可按需显示明文、复制或从剪贴板粘贴；明文仍不写入 `settings.json`。
- 标准包内置 CPU OCR 与 CPU Argos 翻译，不携带 NVIDIA/CUDA/cuDNN、AMD 或 Intel 专有 GPU 运行库；外部本机大模型服务仍可自行使用显卡加速。

## 开发运行

```powershell
dotnet build PingYi.slnx
dotnet run --project src/PingYi.App/PingYi.App.csproj
```

需要直接打开独立设置窗口进行排障时，可在可执行文件后添加 `--settings`。

开发模式下，本地 OCR 不需要 Python；Argos 翻译引擎依赖可用下列命令安装：

```powershell
.\scripts\setup-engine.ps1
```

应用开发模式会自动查找 `engine_host/main.py`。发布时运行 `scripts/publish.ps1`，它会生成裁剪后的自包含 .NET 程序、精简独立引擎并打包标准离线模型。

标准安装包开箱即用：首次启动无需联网，已包含中英 OCR 和中英互译基础模型。模型加载前会校验 SHA-256；“清理下载模型”只清理用户后来下载的翻译模型，不会删除安装包内的保底模型。

## 隐私边界

- 本地 OCR + 本地翻译由网络保护层强制离线；标准安装包首次使用也不需要联网。
- 百度 OCR 会上传用户框选的图片；百度或自定义翻译只上传 OCR 后的文字。
- 正文、截图与密钥不会写入应用日志。结果窗口关闭后不保留历史。

## 当前兼容范围

- Windows 10/11 x64。
- Ubuntu X11 x64；Wayland 截图门户不在 v1 范围。
- OCR/翻译语言为简体中文与英文。

## 测试与发布

```powershell
dotnet test PingYi.slnx
py -3 -m unittest discover -s engine_host -p "test_*.py"
.\scripts\run-quality-baseline.ps1 -ModelDirectory <离线模型目录>
.\scripts\publish.ps1 -Runtime win-x64
```

OCR 的固定场景分数、翻译对比和成品依赖审计见 [质量基线](docs/QUALITY_BASELINE.md)。

Inno Setup 安装在自定义目录时，可向发布脚本传入 `-InnoCompiler "D:\path\to\ISCC.exe"`。

发布机尚无模型源时，先运行 `python scripts/download-offline-models.py --destination artifacts/model-source`，再把该目录传给 `publish.ps1 -OfflineModelSource artifacts/model-source`；下载脚本固定上游版本并校验哈希。Windows 发布生成自包含 ZIP，并可加 `-BuildInstaller` 生成 Inno Setup 安装包。Ubuntu 运行 `scripts/publish.sh` 生成自包含 `.tar.gz` 与 `.deb`。
推送 `v*` 标签时，`.github/workflows/release.yml` 会在 Windows/Ubuntu 运行器分别构建上述产物并创建 GitHub Release。

## 许可证

[MIT](LICENSE)
