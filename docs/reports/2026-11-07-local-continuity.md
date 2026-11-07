# 解压试用包 10 分钟连续翻译补充验收

日期：2026-11-07。使用 `artifacts/local-trial-unpacked-20261106-r2/` 中的完整本机试用包。模型、Python 和翻译进程均从解压目录加载；测试进程以每 100 毫秒一块的节奏送入音频，每分钟播放一次固定英文语音，其余时间为静音。

## 增强的检查

- 将内存采样绑定到本次测试启动的随包 `python.exe`，避免同时存在其他 Python 进程时读错对象。短时双向 WAV 测试先验证进程定位与中英译文。
- 10 分钟内每块音频后检查待处理句队列与 PCM 缓冲，每分钟采样测试进程和翻译进程的私有内存。以内存首个样本（运行约 1 分钟）为基线，两个进程各自的采样最大增长上限为 512 MiB。
- 结束时检查有译文的终稿至少 8 条、语句 ID 无重复、序号严格递增、翻译取消为 0。

## 本机结果

`BundledLocalTrialLiveTests.ExtractedPackageKeepsTranslatingForTenMinutes` 通过，测试运行 10 分 12 秒；测试内音频时钟为 10 分 03 秒，共送入 6,000 块。得到 20 条有译文的终稿、0 次取消；语句 ID 唯一且序号递增。按每块后的采样，待处理队列最多 1 条、PCM 缓冲最多 83,456 字节，低于现有 2 条和 768,000 字节的上界。

从首分钟后的每分钟采样计算，承载 Whisper/VAD 的 .NET **测试进程**私有内存最大增长按 MiB 取整为 34 MiB；随包 Python 翻译进程为 0 MiB（按 MiB 取整）。两者均低于每进程 512 MiB 的回归阈值。旧日志标签 `app-growth-MiB` 指该 .NET 测试进程，测试代码已改名为 `testhost-growth-MiB`，不代表 WPF 窗口的内存。

运行命令使用 `RUN_BUNDLED_LOCAL_STABILITY_TESTS=1` 与 `BUNDLED_LOCAL_ROOT=artifacts/local-trial-unpacked-20261106-r2`，完整输出保存在 Git 忽略的 `artifacts/task6-stability-20261107.log`。两方向真实 WAV 复测也通过：英文 “And so, my fellow Americans.” 得到简体中文字幕，中文天气句得到英文译文。

Release 编译为 0 警告、0 错误；全量默认测试 207 成功、0 失败、19 项按环境条件跳过。`dotnet format --verify-no-changes --no-restore` 与 `git diff --check` 通过。

## 适用范围

这是每分钟约 5 秒语音的低占空比测试。内存按分钟采样，无法排除采样间短暂峰值；队列值按每块写入后采样，并有独立的积压单元测试覆盖满队列丢弃。连续测试检查会话终稿顺序，未驱动 WPF 历史窗口持续 10 分钟。高占空比、多说话人、真实歌曲准确率和物理断网仍需单独现场验收；麦克风完整翻译按项目负责人要求免测。
