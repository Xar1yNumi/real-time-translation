# Windows 实时翻译字幕软件 MVP 设计文档

- 状态：已确认，可进入实施计划阶段
- 日期：2026-10-27
- 开源许可证：MIT
- 首个公开版本：v0.1.0

## 1. 产品目标

开发一款免费开源、简单易用的 Windows 实时翻译字幕软件。用户观看外语视频、直播、参加线上会议或进行面对面交流时，可以选择系统声音或麦克风作为唯一音源，软件自动识别中文、英语或俄语，并把结果翻译成用户选择的目标语言，显示在始终置顶的悬浮字幕条中。

MVP 的核心体验是：

> 首次配置 Azure 凭据后，用户选择音源和目标语言，点击“开始翻译”，屏幕立即进入悬浮字幕模式。

MVP 优先保证系统声音翻译稳定可用；麦克风翻译同样属于发布范围，但验证和实现顺序排在系统声音之后。

## 2. 已确定的产品决策

| 项目 | 决策 |
| --- | --- |
| 平台 | Windows x64 桌面应用 |
| 开发语言 | C# |
| UI 框架 | WPF |
| 运行时 | .NET 10 LTS，发布时固定 SDK 与依赖版本 |
| 开源许可证 | MIT |
| 云端服务 | Azure AI Speech Translation |
| 输入语言 | `zh-CN`、`en-US`、`ru-RU` 自动识别 |
| 目标语言 | 简体中文 `zh-Hans`、英语 `en`、俄语 `ru` |
| 音频来源 | Windows 系统声音或麦克风；单次会话二选一 |
| 识别方式 | 连续流式语音识别与翻译 |
| 字幕形式 | 桌面置顶、可拖动的双语悬浮字幕条 |
| 服务端 | MVP 不建设自有服务器 |
| Azure 凭据 | 用户自行配置并保存在 Windows Credential Manager |
| 数据保存 | 默认不保存音频、识别原文或翻译历史 |

.NET 10 是当前处于活动支持期的 LTS 版本，WPF 仅运行于 Windows，符合本项目的单平台定位。

## 3. MVP 范围

### 3.1 必须实现

1. 首次启动时配置 Azure Endpoint 和 API Key，并测试连接。
2. 后续启动自动加载非敏感设置和安全存储中的凭据。
3. 系统声音与麦克风两种音源可选，但不能同时开启。
4. 自动识别中文、英语或俄语。
5. 用户从中文、英语和俄语中选择一种目标语言。
6. 实时显示临时识别原文，并在分句确认后显示稳定译文。
7. 悬浮字幕条始终置顶、可拖动、不主动抢占其他应用焦点。
8. 支持暂停、恢复、返回设置和关闭软件。
9. 为网络、凭据、Endpoint、音频设备和 Azure 服务异常提供明确提示。
10. 删除已保存 Azure 凭据。
11. 安全释放音频、网络和翻译会话资源。

### 3.2 明确不实现

- 系统声音与麦克风混音。
- 同一句话内部多语言混说的可靠识别。
- 同时输出多个目标语言。
- 字幕历史、导出和云同步。
- 语音合成或翻译配音。
- 本地 AI 模型或第二种云端翻译引擎。
- 自建服务端、账号和付费系统。
- 系统托盘和全局快捷键。
- 多显示器专门布局。
- 复杂主题系统。
- macOS、Linux 和移动端版本。

这些边界用于控制 MVP 风险。后续功能只有在项目负责人明确下达升级要求后才进入新一轮设计。

## 4. 关键能力边界

Azure 连续语言识别会在给定候选列表中判断语言。MVP 要求能够在不同语句之间识别中文、英语和俄语，但不把同一句话内部的语言切换作为验收要求。候选列表以外的语言可能被错误归类为三种候选语言之一，界面不宣称支持候选列表外的语言。

云端响应时间受网络、音频质量、说话节奏和 Azure 服务状态影响。产品目标是正常网络下，多数句子的稳定译文在一句话结束后约 2～4 秒显示，但不对每一次请求作绝对时延保证。

## 5. 用户体验设计

### 5.1 首次启动

```text
启动软件
  → 显示隐私与费用提示
  → 填写 Azure Endpoint 和 API Key
  → 测试连接
  → 测试成功后保存凭据
  → 进入设置窗口
  → 选择音源和目标语言
  → 点击“开始翻译”
  → 显示悬浮字幕条
```

用户必须明确得知：翻译期间音频会发送到 Azure；软件本身免费开源不代表 Azure 服务永久免费。

### 5.2 后续启动

```text
启动软件
  → 读取上次的非敏感设置
  → 从 Credential Manager 读取 Azure 凭据
  → 显示设置窗口
  → 用户点击“开始翻译”
  → 显示悬浮字幕条
```

软件不会在启动后自动上传音频。每次会话都必须由用户点击“开始翻译”触发。

### 5.3 设置窗口

设置窗口只保留 MVP 所需内容：

- Azure Endpoint 与 API Key 的配置、测试和删除入口。
- 音源选择：系统声音或麦克风。
- 当前音频设备名称。
- 目标语言选择：中文、英语或俄语。
- “开始翻译”按钮。
- 云端音频处理和可能产生费用的提示。

默认使用当前 Windows 默认播放设备或默认麦克风。设备在会话中断开或默认设备发生变化时，当前会话停止并提示用户返回设置后重试；MVP 不做会话中的无缝设备迁移。

### 5.4 悬浮字幕条

- 默认显示在主屏幕底部中央。
- 始终置顶，但不主动获取键盘焦点。
- 支持鼠标拖动并记忆上次位置。
- 使用半透明深色背景。
- 上方显示原文，下方显示译文。
- 临时原文使用浅灰色，最终译文使用高对比白色。
- 原文最多两行，译文最多两行；新内容替换旧内容，不无限增长。
- 鼠标移入时显示“暂停”“返回设置”“关闭”按钮。

按钮语义：

- 暂停：停止音频采集和新音频上传，并关闭当前翻译会话。
- 恢复：重新建立翻译会话；连接完成前显示“重新连接中”。
- 返回设置：停止当前会话、释放资源并打开设置窗口。
- 关闭：停止会话、释放全部资源并退出程序。

## 6. 系统架构

```text
设置窗口 / 悬浮字幕条
          │
          ▼
 TranslationCoordinator（状态与生命周期）
     │                       │
     ▼                       ▼
IAudioCaptureSource   ITranslationProvider
     │                       │
     ▼                       ▼
系统回环 / 麦克风      AzureTranslationSession
     │                       │
     └──── 音频缓冲与格式转换 ────┘
                              │
                              ▼
                       SubtitleState
                              │
                              ▼
                       SubtitleViewModel
```

### 6.1 模块职责

#### Desktop UI

包含设置窗口、凭据设置界面和悬浮字幕条。UI 只绑定状态和发送用户意图，不直接调用 Azure SDK 或音频 API。

#### TranslationCoordinator

负责一次翻译会话的完整生命周期：连接、监听、暂停、恢复、重连、停止和错误恢复。它保证任何时刻最多存在一个音频源和一个 Azure 会话。

状态机如下：

```text
Idle → Connecting → Listening
                    ├→ Paused → Connecting
                    ├→ Reconnecting → Listening
                    ├→ Failed → Connecting（用户重试）
                    └→ Stopping → Idle
```

暂停或关闭会取消正在进行的重连和写入任务，避免后台继续上传音频。

#### Audio Capture

`IAudioCaptureSource` 统一系统回环和麦克风输入。实现负责捕获 PCM 音频、格式转换、缓冲和设备异常通知，不负责翻译或 UI。

首个实现包括：

- `SystemLoopbackAudioSource`：捕获当前默认 Windows 播放设备的系统声音。
- `MicrophoneAudioSource`：捕获当前默认麦克风。

音频输出格式以 Azure Push Audio Stream 实际支持格式为准，由技术原型固定为单一 PCM 格式，避免运行中动态变更。

#### Translation Provider

`ITranslationProvider` 创建独立的流式会话；`ITranslationSession` 管理音频输入和结果事件。接口语义为：

```text
ITranslationProvider
  CreateSessionAsync(options, cancellationToken)

ITranslationSession
  StartAsync(cancellationToken)
  WriteAudioAsync(buffer, cancellationToken)
  StopAsync(cancellationToken)

Events
  PartialTranscript
  FinalTranscript
  FinalTranslation
  LanguageDetected
  ConnectionStateChanged
  ErrorOccurred
```

MVP 只实现 `AzureTranslationProvider`。接口用于隔离 Azure SDK，而不是提前开发其他引擎。

#### Subtitle State

保存当前临时原文、最终原文、最终译文、检测语言和显示状态。它不保存历史记录，也不负责持久化。

#### Settings and Credentials

- 非敏感设置保存在当前用户的应用设置目录。
- API Key 只保存在 Windows Credential Manager。
- Endpoint 可以保存在普通设置中，但日志不得包含其查询参数。
- 用户删除凭据时，同时清除内存中的副本。

## 7. 实时数据流

```text
用户点击开始
  → Coordinator 建立 Azure 会话
  → 建立成功后启动唯一音源
  → PCM 音频写入 Azure Push Audio Stream
  → 临时识别事件更新浅色原文
  → 最终识别与翻译事件更新稳定字幕
  → 下一语句替换当前字幕
```

如果检测语言与目标语言相同，UI 显示最终原文，并在译文行显示“与目标语言相同”，不等待额外的二次文本翻译。

所有长时间操作均接收同一个会话级取消令牌。停止、暂停、返回设置和退出都会取消令牌、停止音频源、关闭流、停止 Azure 会话，最后释放资源。

## 8. Azure 集成策略

- 使用 Azure Speech SDK 的连续语音翻译能力。
- 候选源语言固定为 `zh-CN`、`en-US`、`ru-RU`。
- 使用连续语言识别模式，验证不同语句之间的语言切换。
- 翻译目标映射为 `zh-Hans`、`en`、`ru`。
- 用户客户端直接连接 Azure，不经过项目自建服务器。
- 临时原文可以实时更新；译文在 Azure 返回最终分句结果后更新，不强求逐字译文。

正式界面开发前，技术原型必须验证：

1. 三种候选语言的连续识别和不同语句间切换。
2. 三个目标语言代码的翻译结果。
3. 系统回环音频推送到 Azure 的兼容格式。
4. 暂停、恢复和连续运行时的 SDK 行为。

## 9. 安全与隐私

1. 仓库、测试数据、日志和发布包不得包含真实 Azure API Key。
2. 普通配置文件不得保存明文密钥。
3. 默认不保存原始音频、识别原文、译文或字幕历史。
4. 默认日志不记录音频内容、识别原文或译文。
5. 错误日志过滤凭据、认证头和 Endpoint 查询参数。
6. 调试日志只能由用户主动开启，并采用容量受限的滚动文件。
7. 界面在首次配置时明确说明音频发送至 Azure。
8. 用户可以随时删除本机保存的 Azure 凭据。
9. 暂停、返回设置和关闭后不再上传新音频。
10. 应用不静默启动翻译，不在后台自动恢复已由用户停止的会话。

## 10. 异常与恢复

| 情况 | 行为 |
| --- | --- |
| Azure Key 无效 | 停止连接，显示认证失败并返回凭据设置 |
| Endpoint 无效 | 显示配置错误，不启动音频上传 |
| 网络中断 | 停止当前写入，按 1、2、4 秒最多自动重试三次 |
| 三次重试失败 | 进入 Failed，提供“重新连接”和“返回设置” |
| 没有检测到声音 | 保持 Listening，显示“等待音频”，不视为错误 |
| 音频设备不可用或断开 | 停止会话并提示返回设置重试 |
| Azure 服务取消或限流 | 显示可理解的错误分类，允许手动重连 |
| 用户暂停 | 取消重试，停止采集和上传，进入 Paused |
| 用户关闭 | 取消全部任务，释放资源后退出 |

自动重试必须是串行的，并使用会话标识防止旧回调污染新会话。

## 11. 性能和验收标准

### 11.1 性能目标

| 指标 | MVP 目标 |
| --- | --- |
| 临时原文 | 说话过程中尽快出现，记录实测值但不设硬性发布门槛 |
| 稳定译文 | 正常网络下，多数句子在说话结束后 2～4 秒显示 |
| 连续运行 | 至少稳定运行 30 分钟 |
| 语言切换 | 不同语句之间切换候选语言时无需用户重启会话 |
| 资源释放 | 暂停、停止和退出后不继续捕获或上传音频 |

技术报告同时记录平均延迟和 P95 延迟。云端时延目标是产品目标，不构成对 Azure 服务的保证。

### 11.2 发布验收

P0，任一失败即阻止发布：

- 浏览器或会议软件播放的声音可以通过系统回环进入 Azure。
- 中、英、俄三种测试音频分别能够被识别并翻译到另外两种目标语言。
- 不同语句之间切换语言时，会话无需手动重启。
- 设置、字幕、暂停、恢复、返回设置和退出流程可完成。
- Key 不出现在仓库、配置文件和日志中。
- 关键错误均有用户可见提示，应用不崩溃或静默停止。

P1，同样属于 MVP 交付范围，但在 P0 后验证：

- 默认麦克风可以正常识别和翻译。
- 连续运行 30 分钟无崩溃、重复会话或持续增长的资源占用。
- 网络中断、有限重试和手动恢复符合设计。
- 音频设备断开时能够安全停止。

## 12. 测试策略

### 12.1 自动化测试

- Coordinator 状态转换和非法转换。
- 单音源与单翻译会话约束。
- 暂停、停止和取消令牌传播。
- 重试次数、退避顺序及用户停止后不再重试。
- 临时字幕与最终字幕替换规则。
- 两行截断与换行逻辑。
- 设置序列化不包含 API Key。
- 日志脱敏规则。
- 使用假音频源和假翻译会话完成无网络集成测试。

### 12.2 真实环境测试

- 使用自制或允许再分发的中、英、俄语音样本。
- 系统音频：浏览器视频、直播片段和会议播放。
- 麦克风：安静环境和普通室内噪声环境。
- 网络：正常、短暂断网、长时间断网和恢复。
- 生命周期：暂停、恢复、返回设置、关闭、重复启动会话。
- 稳定性：持续运行至少 30 分钟。

真实 Azure 集成测试使用本地安全凭据或 CI Secret，不把密钥写入测试文件；公开 CI 默认运行无密钥测试。

## 13. 代码仓库结构

```text
/
├─ src/
│  ├─ RealTimeTranslation.Desktop/     # WPF、ViewModel、应用启动
│  ├─ RealTimeTranslation.Core/        # 状态机、接口、字幕与业务规则
│  └─ RealTimeTranslation.Infrastructure/ # Windows 音频、Azure、凭据、设置
├─ tests/
│  ├─ RealTimeTranslation.Core.Tests/
│  └─ RealTimeTranslation.IntegrationTests/
├─ docs/
│  ├─ architecture/
│  └─ superpowers/specs/
├─ .github/
│  ├─ workflows/
│  ├─ ISSUE_TEMPLATE/
│  └─ pull_request_template.md
├─ README.md
├─ README.zh-CN.md
├─ LICENSE
├─ CONTRIBUTING.md
├─ SECURITY.md
├─ CHANGELOG.md
├─ .editorconfig
├─ .gitignore
└─ RealTimeTranslation.sln
```

三个生产项目足以隔离 UI、业务规则和外部依赖。MVP 不继续拆分更多项目，避免过度工程化。

## 14. 开源与发布规范

- README 提供功能说明、截图、系统要求、Azure 配置、隐私说明、费用提示和常见问题。
- 中英文文档保持相同的关键安全信息。
- `LICENSE` 使用标准 MIT 文本。
- `CONTRIBUTING.md` 说明构建、测试、提交和 Pull Request 流程。
- `SECURITY.md` 提供私下报告安全问题的方式。
- `CHANGELOG.md` 按版本记录用户可见变化。
- GitHub Actions 在每次 Pull Request 上执行恢复、构建和无密钥测试。
- 发布物包含校验值，并记录构建使用的 SDK 和依赖版本。
- 第一个公开版本使用 `v0.1.0`，不宣称功能已经达到长期稳定版标准。

## 15. 技术验证与交付阶段

1. 建立 .NET 10/WPF 解决方案、测试框架和 CI 基线。
2. 编写最小 Azure 连续翻译原型，验证三种输入与三种目标语言。
3. 实现 Windows 系统回环音频输入并完成端到端翻译。
4. 记录语言切换、延迟、连续运行和 SDK 限制，形成可行性报告。
5. 实现麦克风输入、Coordinator 状态机和异常恢复。
6. 实现凭据、设置窗口和悬浮字幕条。
7. 完成安全、隐私、稳定性和发布验收。
8. 完善开源文档，打包并发布 `v0.1.0`。

如果第 2～4 阶段的 P0 验证失败，应先修订技术方案和本设计文档，不能绕过风险继续堆叠完整界面。

## 16. 完成定义

只有同时满足以下条件，MVP 才算完成：

- 所有 P0 和 P1 验收项通过。
- 真实 Azure 测试记录证明三种语言和三种目标方向可用。
- 正常网络下的译文时延总体符合 2～4 秒体验目标，并公开记录例外情况。
- 连续运行、暂停恢复、网络中断和音频设备断开测试通过。
- 仓库扫描未发现密钥、音频或字幕隐私数据。
- 构建、测试、打包和基础使用文档可由新贡献者复现。
- MIT 许可证和开源治理文件完整。

## 17. 参考资料

- [Azure：实现语言识别](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-identification)
- [Azure：语音翻译](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-translate-speech)
- [Azure Speech 支持的语言](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support)
- [.NET 官方支持策略](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [WPF 概览](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
