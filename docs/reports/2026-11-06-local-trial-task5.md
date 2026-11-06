# Task 5：中英本机试用包验收

日期：2026-11-06。环境：项目开发机 Windows x64；自包含桌面程序、随包 CPython 3.11、Whisper small q5_1、Silero VAD、OPUS-MT 中英双方向。试用 ZIP 保存在 Git 忽略的 `artifacts/`，模型权重不提交仓库。

## 试用包与启动

- 最终包：`artifacts/RealTimeTranslation-local-zh-en-20261106-r2-win-x64.zip`，669,141,389 字节；SHA-256：`B55B7614206B90D0C9BDE5E5427E1385E4FA3930F7FE584C39558473C3BCB1ED`，与相邻 `.sha256` 文件一致。
- 从最终解压目录 `artifacts/local-trial-unpacked-20261106-r2/` 启动 `RealTimeTranslation.Desktop.exe`。设置窗口显示“本机中英翻译组件已就绪，可直接开始”，目标语言仅有简体中文和英语，开始按钮可用。检查后关闭窗口。
- 打包脚本检查四个模型权重的哈希、ONNX Runtime 原生 DLL，以及输出目录中没有 Azure/CognitiveServices 文件。随包包含 README、MIT 许可证、第三方声明及模型目录内许可文件；第三方转依赖的公开分发复核仍未完成，本包仅供项目负责人本机验收。

## 解压包真实翻译

`BundledLocalTrialLiveTests.ExtractedPackageTranslatesWithoutInstalledPythonOrOnlineService` 使用解压目录中的 Python、模型、VAD 和 Whisper，不调用已安装 Python 或在线翻译客户端。固定英文 JFK WAV 与中文天气 WAV 以每 100 毫秒 3,200 字节的节奏输入实际会话；检测源语言分别为 `en` 和 `zh`，两方向都有非空原文和译文，英文样本译文包含“美国同胞”，中文样本译文包含英文字母。测试 1/1 通过，约 20 秒。开发环境另有通过拒绝 HTTP 的两方向 WAV 测试，见 [Task 4 报告](2026-11-04-local-preview-vad.md)。

`BundledLocalTrialLiveTests.ExtractedPackageKeepsTranslatingForTenMinutes` 对最终解压包按真实时间节奏持续送入 PCM，每分钟插入一次英文语音，其余时间送入静音。10 分 12 秒结束；断言至少收到 8 条有译文的终稿，翻译取消数为 0，测试 1/1 通过。该测试不测量峰值内存或字幕 UI 的历史顺序，也不能代表多说话人、高占空比或 30 分钟负载。

## 编译与测试

- `dotnet build RealTimeTranslation.slnx --no-restore --configuration Release -v minimal`：0 警告、0 错误。
- `dotnet test RealTimeTranslation.slnx --no-build --no-restore --configuration Release -v minimal`：207 成功、0 失败、19 条按环境条件跳过；最终解压包双方向 WAV 和 10 分钟测试分别单独通过。
- 独立代码评审发现组件清单遗漏 Python 关键运行文件，以及翻译子进程故障后无法重建。两个回归测试先复现失败，再补充校验清单与一次自动重试；定向测试和全量测试均通过。
- `dotnet format RealTimeTranslation.slnx --verify-no-changes --no-restore` 和 `git diff --check`：通过。

## 验收边界

本机默认路径不构造 MyMemory 或 Azure 客户端，随包模型与运行时可从本地读取；本轮没有实际关闭 Windows 网络适配器，因此物理断网场景仍缺现场证据。默认扬声器系统回环到中文字幕的完整链路已在 Task 4 测过；本轮解压包测试直接向会话输入固定 WAV，并未再次测量系统回环。项目负责人免除麦克风完整翻译现场测试，状态为免测放行，不能记作实测通过。真实中文歌曲的错字率也尚无本轮量化数据。首次预览延迟、歌词伴奏干扰、公开分发的许可复核仍是后续验收事项。
