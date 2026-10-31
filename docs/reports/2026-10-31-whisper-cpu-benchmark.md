# Whisper CPU 单项实测（2026-10-31）

- 环境：Windows，AMD Ryzen 7 5800H，Whisper.net 1.9.1 CPU 运行时。
- 模型：`ggml-small-q5_1.bin`，190,085,487 字节，SHA-256 `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb`。测试前由 `WhisperModelStore` 重新校验。
- 音频：[whisper.cpp 上游 JFK 样例](https://github.com/ggml-org/whisper.cpp/blob/master/samples/jfk.wav)，WAV PCM16/16 kHz/单声道，约 11 秒；文件 SHA-256 `59dfb9a4acb36fe2a2affc14bacbee2920ff435cb13cc314a08c13f66ba7860e`。样例单独下载到 `artifacts/samples`，不纳入 Git。
- 实测：识别 9.25 秒；检测语言 `en`；原文 `And so my fellow Americans, ask not what your country can do for you, ask what you can do for your country.`
- 复现：将模型放入 `artifacts/models`、样例放入 `artifacts/samples` 后，设置 `RUN_WHISPER_LIVE_TESTS=1`，运行 `dotnet test tests/RealTimeTranslation.Infrastructure.Tests/RealTimeTranslation.Infrastructure.Tests.csproj -c Release --filter "FullyQualifiedName~WhisperLiveTests" --show-live-output on --output Detailed`。

这是单条音频、识别阶段的测量，不包含切句等待、网络翻译和字幕绘制。真实短句、中文和俄语的识别质量与端到端延迟仍待后续验收。
