# 实时翻译字幕

[English](README.md)

这是一款 Windows 中英俄实时翻译字幕软件。每次选择一个音源（默认扬声器的系统声音或麦克风）和一种目标语言，悬浮字幕窗持续追加识别原文与译文，可向上滚动查看本次会话。

## 当前短时试用版

默认程序**不需要 Azure 账号、API Key 或其他注册**。Whisper 模型在本机识别语音，识别后的文字经 HTTPS 发给 MyMemory 获取译文。翻译时必须联网。第一次使用需下载并校验约 190 MB 的语音模型，以后可复用；音频不上传。开始前须确认界面上的文字发送说明。

MyMemory [匿名额度为每天 5,000 字符](https://mymemory.translated.net/doc/usagelimits.php)，[单次请求最多 500 UTF-8 字节](https://mymemory.translated.net/doc/spec.php)。服务可用性和译文质量会变化。本试用版尚未通过 30 分钟连续服务验收，也不是公开的 `v0.1.0` 正式版。Azure 代码保留在可选的 `RealTimeTranslation.Azure` 项目中，默认桌面包不包含 Azure SDK。

## 如何使用

在 Windows x64 上解压试用 ZIP，运行 `RealTimeTranslation.Desktop.exe`，不要单独移走其中的文件。

1. 点击“下载或校验语音模型”，等待进度达到 100%。
2. 阅读并勾选发送识别文字到 MyMemory 的说明。
3. 选“系统声音”可翻译默认播放设备中的声音；选“麦克风”可翻译电脑附近的说话声。再选择中文、英语或俄语作为目标语言。
4. 点击“开始翻译”。首次准备显卡识别引擎可能约需 20 秒；准备完成后才开始收音。鼠标移入悬浮字幕窗，可使用暂停、返回设置和关闭按钮。悬浮窗可拖动，滚轮可查看本次会话的历史；新会话会清空历史。

说话过程中，字幕窗底部先显示会修正的**原文预览**；语句确定并收到联网译文后，原文和译文会追加到上方的整场会话记录。预览文字只在本机处理，不会反复发送给 MyMemory。

如果通话时只想翻译对方的声音，可选择系统声音，并让对方语音通过默认播放设备输出。同一播放流中的其他声音也可能被采集；当前版本无法单独筛选应用或说话人。

## 从源码构建

需要 Windows x64 和 [.NET SDK 10.0.301](global.json)：

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
dotnet run --project src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --no-build
```

常规测试会跳过需要真实服务、本机测试音频或 30 分钟采集的项目。已取得的证据和待测项目见[实施计划](docs/superpowers/plans/2026-10-31-no-azure-online-trial.md)与[验收报告](docs/feasibility/no-azure-trial-report.md)。

## 隐私与限制

软件不主动保存音频、识别文字、译文或字幕历史。只有识别文字发送到 MyMemory，并受其[服务条款](https://mymemory.translated.net/terms-and-conditions)约束；短时试用时请避免机密通话。暂停和关闭会停止采集。详情见[隐私说明](docs/privacy.md)。

在有兼容显卡的电脑上，程序优先使用 Vulkan 加速识别；无法使用该后端时保留 CPU 运行库。当前电脑的真实扬声器回环测试中，首个原文预览约在 1.6 秒出现；同一段 5 秒英语音频的最终译文在 7.88 至 12.36 秒完成，网络响应波动明显。完整翻译仍要等待语句结束和网络响应，不能保证固定延迟。处理积压时有界队列会丢弃较早的待识别语句并提示。网络故障会进行有限重试；匿名额度耗尽则停止会话。短句或片段的译文可能不自然。详见[本次实测记录](docs/reports/2026-11-02-progressive-subtitle-trial.md)。当前试用包供项目负责人验收，独立干净电脑和长时服务检查仍待完成。

项目采用 [MIT 许可证](LICENSE)。另见[第三方许可说明](THIRD_PARTY_NOTICES.md)、[贡献指南](CONTRIBUTING.md)、[安全说明](SECURITY.md)和[发布检查清单](docs/release-checklist.md)。
