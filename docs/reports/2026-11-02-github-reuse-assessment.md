# GitHub 实时翻译项目复用评估（2026-11-02）

## 结论

保留当前 .NET/WPF 工程，优先改造实时字幕链路；暂不从零重写，也不整体迁移到另一个仓库。`SakiRinn/LiveCaptions-Translator` 是最接近的现成试用对象，值得单独做中英场景体验对照。其 Windows 实时字幕接入方式和渐进式显示逻辑可作为设计参考，但直接复制代码需要遵守 Apache-2.0 的再分发条款。`LinguaRelay` 的有界队列、增量识别和字幕定稿策略可借鉴；`WhisperLive` 是活跃的流式识别参考实现。以上推荐是源码与仓库活动评估，尚未在本机运行候选软件进行效果验收。

用户本轮放宽首版语言要求：中英互译即可，俄语可后续升级。其余现有要求仍按当前项目理解：Windows、一次选择一个音源、悬浮且累计的会话字幕、联网翻译、免 Azure 和免用户注册/API Key，以及最终以 MIT 开源。

## 候选项目

| 项目 | 最后默认分支提交 / 最近发布 | 适配点 | 缺口与判断 |
| --- | --- | --- | --- |
| [LiveCaptions Translator](https://github.com/SakiRinn/LiveCaptions-Translator) | 2026-08-17 / [v1.7.1300.1822，2026-01-18](https://github.com/SakiRinn/LiveCaptions-Translator/releases/tag/v1.7.1300.1822) | C#、WPF、Windows 11 悬浮字幕与历史记录；内置两个无需用户填写密钥的 Google 翻译方式。中英文语音识别在 Windows 实时字幕支持范围内。 | 基于 Windows 11 实时字幕，使用 UI Automation 读取字幕；系统声始终纳入识别，麦克风为附加选项，不能直接保证当前“一次只选一个音源”交互。两个 Google 方式使用非公开客户端接口，稳定性须实测。Apache-2.0，整体迁移会改变目前的许可归属。仓库仍有 2026-10 的议题/PR 活动，但正式发布滞后。**适合独立试用与局部借鉴。** |
| [Live Translation & Subtitles](https://github.com/fmadore/Live-translation) | 2026-11-02 / [v1.12.0，2026-10-27](https://github.com/fmadore/Live-translation/releases/tag/v1.12.0) | MIT；Windows 系统声、麦克风和进程音源；透明悬浮字幕、完整转录历史；近期持续提交和发布。 | Whisper 本机免密钥翻译只输出英语；中文目标的云翻译要求用户提供 Gemini/OpenAI API Key。Tauri/Rust/TypeScript 与现有 C# 工程差异大。**维护活跃，适合交互对照，不适合整体替换。** |
| [LinguaRelay](https://github.com/MuzeAnisichael/LinguaRelay) | 2026-10-06 / [v0.3.2 alpha，2026-08-26](https://github.com/MuzeAnisichael/LinguaRelay/releases/tag/v0.3.2) | MIT；Windows 系统/进程/麦克风单音源；早期增量识别、短停顿/标点/最长时长定稿；有界队列与历史字幕。支持中英互译。 | Python 栈；核心翻译在本机运行，和“翻译时必须联网”不一致；公开安装包仍是 alpha。**适合借鉴延迟与队列设计，不宜整体移植。** |
| [WhisperLive](https://github.com/collabora/WhisperLive) | 2026-11-01 / [v0.10.0，2026-09-07](https://github.com/collabora/WhisperLive/releases/tag/v0.10.0) | MIT；持续维护的流式语音识别项目，提供 partial/committed transcript 机制。 | Python 服务端/客户端，需要额外进程、模型和发布链路；不能直接替换本项目的 WPF 应用及免密钥联网翻译。**适合作为识别算法或桥接实验参考。** |

维护状态以默认分支提交与发布为主，星标和 `updated_at` 不作为维护证据。LiveCaptions Translator 的 [2026-08-17 提交](https://github.com/SakiRinn/LiveCaptions-Translator/commit/a6fee12757b15edbeef7c60f8895e0be694801e1) 后尚无新的默认分支提交；2026-10 仍有[问题处理](https://github.com/SakiRinn/LiveCaptions-Translator/issues/279)和 PR 活动。[WhisperLive 的 2026-11-01 提交](https://github.com/collabora/WhisperLive/commit/8fb9780ae0252b805cc77581e4ca37827cfd1a14)、[LinguaRelay 的 2026-10-06 提交](https://github.com/MuzeAnisichael/LinguaRelay/commit/acb4b19396f4fd3ef914ad1fb77afa5142fd7641)和[Live Translation & Subtitles 的 2026-11-02 提交](https://github.com/fmadore/Live-translation/commit/4579eb17a5e2338f4a244c7da7bff1d6edfd467b)可核查。

## 关键技术与许可判断

- [微软文档](https://support.microsoft.com/en-us/accessibility/windows/use-live-captions-to-better-understand-audio)列出 Windows 11 实时字幕支持中、英文识别；普通实时字幕的语音识别语言列表没有俄语。Copilot+ PC 自带翻译是另一项硬件受限功能；候选应用自行调用外部翻译，不依赖该功能。
- LiveCaptions Translator 的[识别接入源码](https://github.com/SakiRinn/LiveCaptions-Translator/blob/master/src/utils/LiveCaptionsHandler.cs)通过 UI Automation 读取 Windows 字幕，并会启动/结束系统实时字幕进程。它的[翻译源码](https://github.com/SakiRinn/LiveCaptions-Translator/blob/master/src/apis/TranslateAPI.cs)将 `Google`/`Google2` 标为免配置，但调用的是 Google 客户端使用的端点；这不是有明确服务承诺的官方开发者 API。不能仅凭仓库存在就承诺持续可用或低延迟。
- 本项目已有 NAudio 单音源捕获、Whisper.net 识别、MyMemory 联网翻译、字幕会话历史与测试。需要处理的主要问题是增量识别、字幕定稿/覆盖和可感知延迟。整体替换会重做这些已完成部分，同时引入新架构与发布风险。
- 本项目可保持自己的 MIT 许可。若复制 Apache-2.0 项目代码，需保留原版权与许可证、标明修改，并按[Apache-2.0 第 4 条](https://www.apache.org/licenses/LICENSE-2.0)处理原项目的 NOTICE；不能把复制来的代码直接标成仅 MIT。仅参考设计并独立实现则无需复制源码。

## 建议的下一步

1. 将 LiveCaptions Translator 作为**独立体验对照**：在同一台 Windows 11 电脑、相同中英文语音和网络条件下，实测首次字幕、稳定译文、连续 10 分钟、系统声与麦克风混入、断网提示和历史回看。当前评估尚未运行该候选软件。
2. 在现有工程加入“未定稿字幕持续更新、定稿后追加历史”的双层显示；缩短从语音出现到首个可读文字的时间。先使用当前单音源采集，保留现有 MIT 和测试基线。
3. 若识别延迟仍无法达到体验目标，再试验 Windows 11 实时字幕或 WhisperLive 的流式方案；以可重复的实测数据决定是否切换识别后端。联网翻译接口独立于识别后端，避免再次绑定 Azure。
