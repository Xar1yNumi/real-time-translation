# v0.1.0 Preview 1 — Windows x64

首个可下载的中英实时字幕预览版。ZIP 包含桌面程序、语音识别和翻译模型，以及所需的 Python 运行文件；无需 Azure、账号、API Key 或另装 Python。运行前需要安装[微软官方 Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe)，已有较新 x64 版本者可跳过；ZIP 不含完整的 VC++ x64 安装包。

## 使用方法

1. 安装[微软官方 Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe)，若已安装较新 x64 版本可跳过。
2. 下载 Windows x64 ZIP 和同名 `.sha256` 文件，核对 ZIP 的 SHA-256。
3. 完整解压 ZIP，运行其中的 `RealTimeTranslation.Desktop.exe`，不要单独移动 EXE。
4. 选择“系统声音”或“麦克风”，再选择目标语言“简体中文”或“英语”，点击“开始翻译”。
5. 悬浮窗会持续追加本次会话的原文和译文；滚动可查看之前的字幕，关闭程序后记录清空。

## 范围与已知限制

- 只支持中文和英文。系统声音采集默认播放设备的整个音频流，暂不区分应用或说话人。
- 歌曲、背景噪声和多人同时说话会降低识别准确率；译文有时会改写或漏掉部分短语。
- 完整麦克风翻译、真实歌曲准确率、物理断网、30 分钟高负载运行均未现场验收。运行中的音频和文字不会默认写入磁盘。
- 本预览版按 Windows x64 制作；完整测试与依赖来源见 [发布验证记录](reports/2026-11-12-public-binary-release.md) 和 [第三方声明](../THIRD_PARTY_NOTICES.md)。

The first downloadable Windows x64 Chinese–English subtitle preview. Install the [official Microsoft Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe) first unless a recent x64 version is already present. Extract the whole ZIP and run `RealTimeTranslation.Desktop.exe`; bundled models and Python need no account, API key or Azure service. Select system audio or microphone and a target language. The overlay appends this session's original text and translations. Music, background noise and multiple speakers can reduce accuracy. Per-app audio, history export and editing are outside this preview. The full microphone path, song accuracy, physical network disconnection and a 30-minute heavy-load run have not been field tested.
