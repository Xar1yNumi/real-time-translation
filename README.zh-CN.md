# 实时翻译字幕

[English](README.md)

这是一个面向 Windows 的实时翻译字幕项目，目标是在观看外语视频、直播或参加线上会议时，把中英俄语音显示为屏幕上的双语悬浮字幕。你可以选择**系统声音**，翻译电脑正在播放的语音；也可以选择**麦克风**，翻译身边的语音。一次会话只使用一个音源和一种目标语言。

> **当前状态：**项目仍在开发 MVP，尚不是经过完整验收的正式版本。Azure 端到端翻译、断网恢复和云端连续运行仍待实测，目前也没有可下载的正式安装包。

## MVP 计划提供的功能

- 在不同语句之间识别 `zh-CN`、`en-US` 或 `ru-RU`，并翻译为简体中文、英语或俄语。
- 在置顶、可拖动的字幕条中显示临时识别原文和最终译文；原文与译文区域各最多显示两行。
- 在悬浮字幕条中暂停、恢复、返回设置或关闭软件。
- 从 Windows 默认播放设备或默认麦克风中选择一个音源。系统音源采集的是**默认设备的全部播放声音**，电脑同时播放的其他声音也可能被采集。
- 将 Azure API Key 保存在 Windows 凭据管理器中，并提供删除入口。

语音识别和翻译流程已有代码实现，但三语能力和实际体验**尚未用真实 Azure Speech 资源验证**。已取得的证据与待测项目见[技术可行性报告](docs/feasibility/azure-audio-report.md)和[悬浮字幕交互检查点](docs/feasibility/subtitle-ux-checkpoint.md)。

## 运行条件

- Windows x64。
- 从源码构建需要 [.NET SDK 10.0.301](global.json)。
- 自备 Azure AI Speech 资源、HTTPS Endpoint 和 API Key；翻译时需要联网。
- 可用的默认播放设备或麦克风。

软件计划免费开源，但**Azure 服务可能根据你的订阅产生费用**。开始使用前，请核对自己的 Azure 价格和用量限制。

## 从源码构建和运行

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
dotnet run --project src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --no-build
```

在设置窗口中：

1. 填入 Azure Speech 资源的 Endpoint 和 API Key，点击“测试连接”。连接测试对有效和无效真实凭据的行为仍待验证。
2. 选择“系统声音”或“麦克风”，再选择目标语言。
3. 点击“开始翻译”。只有点击后才开始采集音频；鼠标移入悬浮字幕条可显示操作按钮。

如果你在电脑上通话，只想翻译对方的声音，可以选择系统声音，并让对方语音通过电脑的默认播放设备输出。本 MVP 无法从同一播放流中单独分离某个应用或说话人的声音。

## 隐私

翻译会话进行期间，音频从本机直接发送到 Azure AI Speech；项目没有自建服务器。软件本身不主动保存采集音频、识别原文、译文或字幕历史。Endpoint 等非敏感设置保存在当前用户的本地应用数据目录，API Key 保存在 Windows 凭据管理器。`%LOCALAPPDATA%/RealTimeTranslation/logs` 中有容量受限的诊断日志，只记录事件名、错误类别、时间戳和允许的数字指标。不要把真实密钥写入仓库、测试文件、日志或问题报告。详情见[隐私说明](docs/privacy.md)。

## 故障排查

- **连接测试失败：**检查 Endpoint 是否为 HTTPS、密钥是否属于你的 Azure Speech 资源，以及网络能否连接 Azure。认证和网络错误会显示不同的安全提示；真实服务行为仍待验证。
- **没有出现文字：**检查所选音源和 Windows 默认设备。系统声音只采集默认播放流；静音时也可能产生音频帧。
- **断线后字幕停止：**网络或服务故障只会按 1、2、4 秒进行有限重试。最终失败时请返回设置。真实断网恢复仍待实测。
- **Azure 费用异常：**暂停或关闭会话，并在自己的 Azure 订阅中核对用量。

截图将在 Windows 界面复核后补充；当前交互检查见[悬浮字幕检查点](docs/feasibility/subtitle-ux-checkpoint.md)。

## 项目进度

项目已有本地自动化测试和 Windows 音频采集测试，其中包含一次 **30 分钟仅采集**的系统回环测试。这些结果不能证明云端翻译稳定。公开发布 `v0.1.0` 前，还需完成中英俄真实 Azure 测试、延迟测量、断网与恢复、悬浮窗交互，以及至少 30 分钟端到端翻译实测。后续工作见[MVP 实施计划](docs/superpowers/plans/2026-10-27-real-time-translation-mvp.md)。

项目采用 [MIT 许可证](LICENSE)。另见[贡献指南](CONTRIBUTING.md)、[安全报告说明](SECURITY.md)、[变更记录](CHANGELOG.md)和[发布检查清单](docs/release-checklist.md)。公开发布 `v0.1.0` 仍需完成尚缺的真实服务检查，并取得项目负责人的发布确认。
