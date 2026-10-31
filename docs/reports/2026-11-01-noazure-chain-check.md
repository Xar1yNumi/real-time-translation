# 无 Azure 音频到字幕链路检查（截至 2026-11-01）

在这台 Windows 电脑上，用 [Whisper 上游公开英文 WAV](https://github.com/ggml-org/whisper.cpp/blob/master/samples/jfk.wav) 向 `NoAzureTranslationSession` 输入 PCM；本机 Whisper 识别短句后，通过 MyMemory 匿名 HTTPS 接口生成中文字幕。创建会话时 Endpoint 和 API Key 均为空。一次完整通过的运行中，首个结果检测语言为 `en`，原文 8 字符，译文 4 字符；从开始写入预录 PCM 到首条最终字幕为 11.14 秒。

本次测试把预录音频一次性写入，并非按真实播放速度输入，因此 11.14 秒不能当作现场说话结束到字幕出现的延迟。此前一次相同链路在在线翻译阶段返回 `NetworkUnavailable`；再次尝试可成功。匿名服务的波动仍需桌面现场验收。

合成音频测试连续送入 100 句时，待识别队列保持最多 2 句，丢弃较早语句并发出提示；停止会取消在途任务。Release 构建、整套测试与格式检查另作任务门槛记录。此处尚未证明中文、俄语真实语音、系统回环现场、麦克风现场及长时间运行。
