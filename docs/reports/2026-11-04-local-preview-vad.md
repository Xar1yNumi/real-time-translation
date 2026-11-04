# Task 4：本地语音分句与原文预览实测

日期：2026-11-04。环境：项目开发机 Windows x64；Whisper small q5_1 本机识别，OPUS-MT 双方向本机翻译，Silero VAD 本机分句。运行时测试通过拒绝外发 HTTP 的客户端进行，模型已在本机缓存。

## 改动与依据

- 原 RMS 只比较音量，5,000 幅度的 440 Hz 纯音会被当成语音。改用 Silero VAD 的 16 kHz、32 ms ONNX 推理；同一模型测试中，纯音 0/150 帧判为语音，JFK 语音 88/156 帧，语音叠加纯音 88/156 帧。
- `silero_vad.onnx` 取自 `snakers4/silero-vad` 的 `60b7ffa243625ebdc1070275a29f18c87843786a`，SHA-256 为 `1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3`。ONNX Runtime 管理包固定 1.23.2，原生 DLL 取自同版本 Python wheel，SHA-256 为 `93457C4F17904D0AB98F3E9AC27ED153993F4B5B769337CBFD6F4C528FB0A509`。模型与运行环境保存在 Git 忽略的 `artifacts/`，发布包仍须在 Task 5 完成准备和许可说明。
- 分句保留 0.16 秒前滚、最少 0.32 秒语音、0.48 秒尾部静音；首个预览在约 0.64 秒有效语音后请求，以后每约 0.8 秒更新一次。单句 PCM 上限 256 KB，待翻译句队列最多 2 个。
- 修复同一句的新音频到来时取消正在进行的 Whisper 预览；终稿仍会取消过时预览。修复本地翻译进程的 UTF-8 输入输出，防止简体中文乱码。英文→中文模型对 “And so, my fellow Americans.” 的单束输出只有“美国人”；改用 2 束后输出“所以，我的美国同胞们”。

## 真实音频结果

JFK 英文 WAV 5.00 秒、中文天气 WAV 5.67 秒，以每 100 ms 一块的节奏喂入实际会话，各方向 3 次。以下 P50 为 3 个值的中位数，P95 为最近秩法的最大值；样本量小，仅用于发现本机回归，不代表长期稳定性。

| 方向 | 开始送音频→首次非空原文预览 P50/P95 | VAD 输出终稿片段→译文终稿 P50/P95 | 终稿内容 |
| --- | --- | --- | --- |
| 英→简中 | 2.70 / 2.78 秒 | 0.93 / 1.24 秒 | “And so, my fellow Americans.” → “所以，我的美国同胞们。” |
| 中→英 | 1.40 / 1.44 秒 | 0.66 / 0.69 秒 | “今天天气很好，我们可以一起去公园散步。” → “It's a nice day. We can go for a walk in the park.” |

VAD 在连续 0.48 秒静音后才输出终稿片段；表中的终稿延迟从该时刻算起。以 VAD 最后一次判为语音的时刻估计，语句结束到译文显示还需额外约 0.48 秒；这不是人工标注的真实语句终点。

系统声完整链路另测一次：通过默认扬声器播放 5 秒 JFK WAV，由 Windows loopback 捕获，进入 Silero、Whisper、OPUS-MT 和字幕状态。首次非空原文预览 3.57 秒，简体中文终稿 3.69 秒；无 Azure 或 MyMemory 请求。这表明首次预览仍偏晚，后续试用验收需继续改善冷启动和实际观看体验。

## 验证范围

- `dotnet build RealTimeTranslation.slnx --no-restore -m:1 -p:UseSharedCompilation=false -v quiet`：0 警告、0 错误。
- Core、Infrastructure、Desktop、Integration 四个 .NET 测试项目：本机正常权限运行均退出 0；需人工触发的旧在线及设备测试按各自条件跳过。两方向本地 WAV 3 轮和系统 loopback 完整链路分别开启并通过。
- Python worker 单元测试 3 项通过；`dotnet format --verify-no-changes --no-restore` 与 `git diff --check` 通过。
- 麦克风 PCM 基础采集曾获得非零样本；完整麦克风→本地翻译本阶段按项目负责人指示免测，状态为**未实测**。不能以现有数据断言麦克风体验达标。
- 合成纯音用于 VAD 抗误判，不能替代真实歌曲的人声识别验收。当前报告也不覆盖 10 分钟连续运行、桌面界面接入或模型首次准备；这些属于 Task 5。
