# 中英本地互译与连续字幕实施计划

状态：草案，设计确认后执行。

> 每项任务按失败测试、最小实现、编译、相关测试、全量测试和独立 Git 提交执行；本计划在桌面仓库的开发分支实施。

**目标：** 首次准备模型后，中英双向语音字幕不依赖 Azure、MyMemory 或运行时网络。

**架构：** 保留现有 WPF 与 NAudio；Whisper 指定源语言识别；独立常驻本地翻译进程只处理定稿文字；语句 ID 保证预览、终稿和历史关联。

**技术：** .NET 10、WPF、NAudio、Whisper.net；候选 M2M100 418M 与 CTranslate2。参见[设计](../specs/2026-11-03-local-zh-en-captions-design.md)。

## 全局约束

- Windows x64；音源一次只选系统声音或麦克风。
- 首阶段只提供中文→英语、英语→简体中文两个方向；手动选择方向。
- 默认会话音频、文字、历史不落盘；模型权重不进 Git。
- 不复制 LinguaRelay 源码；记录模型和运行库许可证。
- 歌曲场景单列验收，不能以普通语音成绩替代。

## Task 1：指定源语言的语音识别

**文件：** `src/RealTimeTranslation.Infrastructure/LocalSpeech/WhisperTranscriber.cs`、`tests/RealTimeTranslation.Infrastructure.Tests/WhisperTranscriberTests.cs`。

**接口：** 新增 `SourceLanguage`（中文、英语）和创建指定语种转写器的工厂重载；保留旧自动检测调用供现有试用版使用。

- [ ] 增加中、英文指定语种测试，记录 FakeInference 收到的语言参数；运行目标测试确认新 API 缺失而失败。
- [ ] 添加最小工厂重载，把 `zh`／`en` 传给 `NativeWhisperInference` 的 `WithLanguage`，其他状态及简体转换沿用旧逻辑。
- [ ] 编译解决方案，运行目标测试和全部测试，`dotnet format --verify-no-changes`，独立提交。

## Task 2：本地机器翻译可行性门槛

**文件：** 新建 `tools/LocalTranslation.Probe/` 和 `docs/reports/local-zh-en-model-benchmark.md`；模型仅存 `artifacts/models/`。

**接口：** 输入方向与 UTF-8 文本，输出译文、模型版本、冷启动／热推理耗时和错误，不依赖在线 API。

- [ ] 固定 `facebook/m2m100_418M` 提交、CTranslate2／SentencePiece／Python 版本，核对模型、权重和依赖许可证与哈希；准备两方向人工可读短句及空句、长句测试。
- [ ] 用 `uv` 准备隔离运行环境，转换并校验模型；在当前电脑分别测中→英、英→中冷启动、热运行 P50/P95、内存和断网调用。
- [ ] 只有两方向译文可读、断网成功且处理速度可供实时使用，才通过接入门槛；否则提交失败证据并调整模型方案，不把未经验证的翻译器接到 UI。
- [ ] 运行探针和许可检查，独立提交源码与指标报告，忽略权重和环境文件。

## Task 3：常驻本地翻译进程与会话接入

**文件：** 新建 `src/RealTimeTranslation.Infrastructure/LocalTranslation/` 的进程客户端及协议；修改 `NoAzureTranslationProvider.cs`、`NoAzureTranslationSession.cs` 对应的抽象接入；新增基础设施测试。

**接口：** `ILocalTextTranslator.TranslateAsync(string text, SourceLanguage source, CancellationToken)`；请求／响应带唯一 ID，只有终稿发给翻译进程。

- [ ] 写失败测试覆盖两方向、空输入、乱序响应、超时、进程退出、取消和旧会话回流。
- [ ] 实现限长 JSON 行协议、常驻进程、关闭与错误分类；用任务 2 的模型服务进程完成真实译文对照。
- [ ] 编译、运行目标和全部测试，再运行真实中英 WAV 端到端及断网测试，独立提交。

## Task 4：更早的原文预览与可靠定稿

**文件：** `PcmUtteranceSegmenter.cs`、`NoAzureTranslationSession.cs`、相应测试与延迟报告。

- [ ] 用许可样本构造有停顿、连续语音、背景音乐的失败用例，记录当前 RMS 分句的错切、漏段和延迟；先验证基线失败。
- [ ] 引入经校验的本地 VAD 或证明更简单策略能通过同一组用例；保留有界队列和语句 ID，过时预览可替换、终稿只追加一次。
- [ ] 运行真实系统声音和麦克风测试，记录音频开始→首个预览、语句结束→最终译文 P50/P95；编译、全量测试、独立提交。

## Task 5：中英界面、模型准备与试用包

**文件：** `SettingsViewModel.cs`、`SettingsWindow.xaml`、`App.xaml.cs`、README、隐私／第三方许可文档及桌面测试。

- [ ] 先写失败测试：只允许两个方向；模型不齐时禁止开始并显示下载信息；断网已有模型时仍能开始；字幕持续累计。
- [ ] 实现方向选择、模型进度与本地状态，默认路径不调用 MyMemory；公开说明初次下载、内存和歌曲限制。
- [ ] 从解压目录启动自包含包，分别验收系统声、麦克风、中英两方向、断网和连续 10 分钟；运行编译、全量测试、格式检查，独立提交并推送开发分支。

单应用音源与历史编辑／导出按设计文档列为后续独立阶段，不在首阶段的“完成”声明中混入。
