# MyMemory 匿名翻译单项实测（2026-10-31）

本机未配置账号、API Key 或 Azure。通过 `MyMemoryTranslator` 向[官方 Get 接口](https://mymemory.translated.net/doc/spec.php)发送六个无敏感信息的短语；六次均返回非空、与原文不同的译文。

| 方向 | 耗时 |
| --- | ---: |
| 英语 → 中文 | 1139 ms |
| 英语 → 俄语 | 552 ms |
| 中文 → 英语 | 441 ms |
| 中文 → 俄语 | 975 ms |
| 俄语 → 英语 | 804 ms |
| 俄语 → 中文 | 421 ms |

测试输入分别为 `Good morning.`、`早上好。`、`Доброе утро.`。复现时设置 `RUN_MYMEMORY_LIVE_TESTS=1`，运行 `dotnet test tests/RealTimeTranslation.Infrastructure.Tests/RealTimeTranslation.Infrastructure.Tests.csproj -c Release --filter "FullyQualifiedName~MyMemoryLiveTests" --show-live-output on --output Detailed`。

这是短时接口可达性检查，不包含语音识别、整链路延迟或持续运行结论。[官方额度说明](https://mymemory.translated.net/doc/usagelimits.php)列出的匿名上限为每天 5,000 字符；服务状态和额度可能变化。
