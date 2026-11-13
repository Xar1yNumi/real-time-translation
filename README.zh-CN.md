# 实时翻译字幕

[English](README.md)

这是一个 Windows 中英双向实时字幕项目。选择系统声音或麦克风，以及想看的语言（简体中文或英语）；程序自动判断原语种，在悬浮窗持续追加本次会话的原文和译文。

## Windows x64 预览版

语音识别、分句和文字翻译都在本机运行；无需 Azure、MyMemory、账号或 API Key。试用包自带模型与 Python 运行文件，用户无需另装 Python。**运行前须安装[微软官方 Microsoft Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe)；若电脑已有较新的 x64 版本可跳过。** ZIP 不含完整的 VC++ x64 安装包；Python 自带所需的 `vcruntime140` 文件。安装此前提后，翻译无需联网；首次打开会校验文件，首次启动识别引擎可能较慢。

目前只支持中文和英语之间互译。若识别出的语言与目标语言相同，则显示原文；其他语言不会猜测译文。普通讲话、视频和歌曲使用同一流程，伴奏或噪声可能造成明显错字；译文也可能改写或漏掉部分短语。单应用音源、历史编辑和导出属于后续阶段。

## 如何试用

完整 Windows x64 试用包发布后可从 [Releases](https://github.com/Xar1yNumi/real-time-translation/releases) 下载；也可按下方“从源码构建”自行准备。ZIP 已包含本机模型和 Python 运行环境。

1. 若尚未安装，先从[微软官方下载并安装 Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe)。
2. 在 Windows x64 上完整解压试用 ZIP，运行 RealTimeTranslation.Desktop.exe，不要单独移动 EXE。包内另有 `START-HERE.txt`。
3. 等待界面显示“本机中英翻译组件已就绪”。若提示组件不完整，请重新解压完整文件夹；若提示缺少 VC++ 运行库，请按弹窗地址安装。
4. 选择“系统声音”翻译默认扬声器的播放内容，或选“麦克风”翻译附近说话声；再选择“简体中文”或“英语”作为目标。
5. 点击“开始翻译”。悬浮窗先显示可修正的原文预览；定稿后把原文和译文追加到上方历史。鼠标移入可暂停、返回设置或关闭；滚轮可查看当前会话记录。关闭程序后历史清空。

通话时若只需翻译对方语音，让对方声音通过默认播放设备输出并选“系统声音”。当前版本会一并采集该播放流里的其他声音，不能按应用或说话人单独筛选。

## 当前验证情况

[公开二进制发布记录](docs/reports/2026-11-12-public-binary-release.md)列出新 CPU 推理库、模型来源、打包检查和实测结果。[早期许可核对记录](docs/reports/2026-11-07-binary-license-audit.md)针对的是另一个未公开发布的旧候选包。

新版 CPU 试用包解压后，已用英文和中文 WAV 跑通语音识别到翻译字幕的完整链路，并通过 10 分钟低占空比连续运行测试。旧包的默认扬声器回环记录见[连续运行记录](docs/reports/2026-11-07-local-continuity.md)。麦克风完整翻译按项目负责人决定免测，不能记为实测通过；真实歌曲准确率和物理断网尚未现场验证。

## 从源码构建

需要 Windows x64 和 [.NET SDK 10.0.301](global.json)。普通 dotnet build 不会下载模型或构造试用包。可先按下列命令编译并运行不依赖模型的测试。若要运行真实翻译或制作完整包，按[完整本机版构建说明](docs/build-local-preview.md)准备 Git 忽略的模型和 Python 文件；模型来源及限制另见[可行性报告](docs/reports/local-zh-en-model-benchmark.md)。

    dotnet restore RealTimeTranslation.slnx --locked-mode
    dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
    dotnet test RealTimeTranslation.slnx --configuration Release --no-build

准备模型后，运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package-local-trial.ps1` 制作完整试用包。脚本核对固定的模型和本机推理库哈希，不负责下载输入文件。

打包脚本会生成 `artifacts/RealTimeTranslation-local-zh-en-win-x64.zip` 和相邻的 `.sha256`。模型和运行环境均不进入 Git。发布状态见[发布清单](docs/release-checklist.md)。

## 隐私与许可

音频、文字和本次会话历史默认不落盘；暂停和关闭会停止采集。当前默认桌面路径不向在线翻译服务发送文字。详情见[隐私说明](docs/privacy.md)。项目代码采用 [MIT 许可证](LICENSE)；模型和依赖分别适用其[第三方许可](THIRD_PARTY_NOTICES.md)。
