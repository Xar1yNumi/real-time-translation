# 中英本地机器翻译模型可行性报告

状态：首个候选 M2M100 未通过译文质量门槛；中英专用 OPUS-MT 双模型通过文字翻译可行性门槛。桌面程序接入及音频端到端验收尚未进行。

## 范围与来源

- 设备：Windows x64、NVIDIA GeForce RTX 3060 Laptop GPU（6 GiB 显存）、16 GiB 内存。
- 候选模型：[facebook/m2m100_418M](https://huggingface.co/facebook/m2m100_418M/tree/55c2e61bbf05dfb8d7abccdc3fae6fc8512fd636)，固定修订版 `55c2e61bbf05dfb8d7abccdc3fae6fc8512fd636`，MIT。
- 本轮测试的转换产物：[LinguaRelay v0.1.5 模型归档](https://github.com/MuzeAnisichael/LinguaRelay/releases/tag/v0.1.5)。其[清单](https://github.com/MuzeAnisichael/LinguaRelay/blob/main/packaging/model-manifest.json)注明同一源模型修订版、权重文件哈希与 MIT 许可。`tools/LocalTranslation.Probe/model-manifest.json` 固定归档和七个翻译文件的 SHA-256。哈希核对只证明与发布内容一致；转换过程未在本项目独立复现。
- 探针环境：Python 3.11.17，CTranslate2 4.8.4（MIT）、SentencePiece 0.2.1（Apache-2.0）、opencc-python-reimplemented 0.1.7（Apache）、psutil 7.1.2（BSD-3-Clause）。版本固定在 `tools/LocalTranslation.Probe/requirements.txt`。
- 处理链：仅原文→译文。中文输出经 OpenCC 转简体。该探针不含音频采集、Whisper 识别、分句或 WPF 绘制，因此不能代表端到端字幕延迟。

## 运行结果

| 检查项 | 结果 |
| --- | --- |
| 归档 SHA-256 | 与 GitHub 发布页 `5c363c4a834d0b82a61ac4fc10872387200b54db763e4e575a80cd5dabdc7638` 一致 |
| 七个翻译文件 SHA-256 | 与固定清单逐项一致 |
| 中文→英文 8 句 | 8 句非空；基本可读，个别措辞生硬 |
| 英文→简体中文 8 句 | 8 句非空；至少 3 句有明显错译，未通过 |
| 空句与超过 256 token 的句子 | 探针单元测试通过；空句不送模型，长句明确报错 |
| CPU 冷启动与热运行 P50／P95 | 加载 3.66 秒；中→英 328／500 ms，英→中 303／434 ms（beam 2）；beam 5 时分别为 406／634 ms 和 396／546 ms |
| CUDA 冷启动与热运行 P50／P95 | 本机缺少 `cublas64_12.dll`，推理启动失败；未将其记为 GPU 性能结果 |
| 进程内存与 GPU 显存 | CPU 模式加载后 RSS 约 545 MiB，运行后约 552 MiB；GPU 显存总占用基本未变 |
| 与 Whisper 同时运行 | M2M100 因译文质量失败未继续测；见下方 OPUS-MT 双模型对照 |
| 断网运行 | M2M100 因译文质量失败未继续测；见下方 OPUS-MT 双模型对照 |

M2M100 的英译中质量未通过。固定样例中，`Please get off at the next stop.` 被译成“请在下一个停车场离开”，`I am testing real-time translation captions.` 被译成“我正在测试实时翻译容器／胶囊”，`Can the application continue when the network is disconnected?` 被译成“网络连接后,应用程序可以继续吗?”，最后一例颠倒了断网条件。提高到 beam 5 并设置重复惩罚后，关键错译仍在。先试同机中英专用候选，再决定模型接入；此时现有在线试用版保持独立可用。

## 中英专用 OPUS-MT 对照

改用 [Helsinki-NLP/OPUS-MT](https://github.com/Helsinki-NLP/Opus-MT) 的两个单方向模型，由 [Live Translator 模型仓库](https://huggingface.co/Unbakedcookie/live-translator-models/tree/50473df5681764ebdef107dd78f7d21da66c28a7)转换为 CTranslate2。固定发布提交 `50473df5681764ebdef107dd78f7d21da66c28a7`，归档核对如下：

| 方向 | 原模型提交 | 归档 SHA-256 | 模型许可 |
| --- | --- | --- | --- |
| 英→中 | `408d9bc410a388e1d9aef112a2daba955b945255` | `1748dfa40aadf2aea67eff4d6845d8aabbd3093604fc22125b7232f17f404947` | CC-BY-4.0 |
| 中→英 | `cf109095479db38d6df799875e34039d4938aaa6` | `5aa44cb302ea85132b041b5c5dd7e76dbad92aa4cc4537674ee4389a0dbd3cef` | CC-BY-4.0 |

两个 ZIP 的实际 SHA-256 均匹配发布清单，包内 `opus_mt.json` 的原模型提交、方向与转换包声明匹配。转换过程由第三方执行，本项目没有独立重新转换。**2026-11-07 许可复核补充：**[英→中原模型的固定修订版](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh/tree/408d9bc410a388e1d9aef112a2daba955b945255)在上游模型页标为 Apache-2.0，与转换包 `LICENCE.txt` 的 CC-BY-4.0 不一致；[中→英原模型](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en/tree/cf109095479db38d6df799875e34039d4938aaa6)标为 CC-BY-4.0。上表记录的是当时转换包声明，不能当作公开二进制分发的最终许可结论。必须先核对并修正英→中的许可元数据、保留两方向的作者和转换归属，并完成其余依赖审计。

首次使用 `source.spm` 编码后没有加 `</s>`，中→英出现整句重复。补上结束 token 后，用同一组八句重新测试，两方向均非空、语义基本正确，英→中还需以 OpenCC 统一简体。英→中“网络断开”不再被译反；“实时翻译字幕”译为“我在测试实时翻译字幕”。中文问候句的“Hello”在英→中输出中被省略，尚有细节误差，不能据这八句宣称通用翻译质量已经验收。

| 指标 | 中→英 | 英→中 |
| --- | ---: | ---: |
| 单模型 CPU 加载 | 556 ms | 539 ms |
| 单模型进程 RSS（推理后） | 137 MiB | 136 MiB |
| 热推理 P50／P95（每句，固定八句、重复三轮） | 49／78 ms | 35／59 ms |

同一进程先后加载两方向模型后 RSS 约 221 MiB。保持两个模型常驻期间，现有 Whisper.net Vulkan 使用 `ggml-small-q5_1.bin` 识别 `jfk-first-5s.wav` 成功：首轮约 19.6 秒，第二轮约 0.54 秒；独立对照分别为 20.9 秒和 0.48 秒。首轮长时间是 Whisper 预热，仍需在应用启动时处理；这个并行测试仅证明一次共存成功，不能证明 10 分钟稳定性。

设置 `HF_HUB_OFFLINE=1`、`TRANSFORMERS_OFFLINE=1`、失效的 HTTP 代理，并在 Python 进程中拒绝 socket 连接时，两方向模型仍加载、完成全部 16 句翻译，退出码 0。这说明本轮推理没有依赖 Python 网络调用；尚未进行系统网卡物理断开测试。

## 接入判断

本机文字翻译门槛通过，下一任务可用 OPUS-MT 双模型构建常驻本地进程；不采用 M2M100 作为默认翻译器。先完成进程协议、端到端中英 WAV 与异常恢复，才能说桌面软件可本地翻译。歌曲、连续字幕和 10 分钟稳定性仍属于后续任务验收。
