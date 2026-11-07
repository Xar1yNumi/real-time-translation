# 实时翻译字幕

[English](README.md)

这是一个 Windows 中英双向实时字幕项目。选择系统声音或麦克风，以及想看的语言（简体中文或英语）；程序自动判断原语种，在悬浮窗持续追加本次会话的原文和译文。

## 当前公开源码预览版

语音识别、分句和文字翻译都在本机运行；无需 Azure、MyMemory、账号或 API Key。完整试用包自带模型与 Python 运行文件，用户无需另装 Python。解压后运行可断网；首次打开会校验文件，首次启动识别引擎可能较慢。

目前只支持中文和英语之间互译。若识别出的语言与目标语言相同，则显示原文；其他语言不会猜测译文。普通讲话、视频和歌曲使用同一流程，伴奏或噪声可能造成明显错字。单应用音源、历史编辑和导出属于后续阶段。

## 如何试用

以下步骤适用于本机已制作的完整试用包。公开仓库目前只有源码，GitHub 暂无可下载的完整安装包；请按下方“从源码构建”准备运行文件。

1. 在 Windows x64 上完整解压试用 ZIP，运行 RealTimeTranslation.Desktop.exe，不要单独移动 EXE。
2. 等待界面显示“本机中英翻译组件已就绪”。若提示组件不完整，请重新解压完整文件夹。
3. 选择“系统声音”翻译默认扬声器的播放内容，或选“麦克风”翻译附近说话声；再选择“简体中文”或“英语”作为目标。
4. 点击“开始翻译”。悬浮窗先显示可修正的原文预览；定稿后把原文和译文追加到上方历史。鼠标移入可暂停、返回设置或关闭；滚轮可查看当前会话记录。关闭程序后历史清空。

通话时若只需翻译对方语音，让对方声音通过默认播放设备输出并选“系统声音”。当前版本会一并采集该播放流里的其他声音，不能按应用或说话人单独筛选。

## 当前验证情况

公开二进制包仍待许可核对；[二进制许可核对记录](docs/reports/2026-11-07-binary-license-audit.md)列出已测试的候选包和发布前待办。

解压试用包已从桌面启动，并用英文、中文 WAV 分别得到两方向译文；10 分钟低占空比连续翻译测试通过。默认扬声器播放英文样本后，经系统回环采集产生了简体中文字幕。[Task 4 实测记录](docs/reports/2026-11-04-local-preview-vad.md)包含预览和终稿延迟，[Task 5 试用包记录](docs/reports/2026-11-06-local-trial-task5.md)列出包内容，[连续运行补充记录](docs/reports/2026-11-07-local-continuity.md)列出队列、顺序和内存实测。麦克风完整翻译按项目负责人决定免测放行，不能记为实测通过；真实歌曲准确率和物理断网仍缺现场数据。当前仓库按**源码预览版**公开，现有试用 ZIP 还需完成第三方分发许可核对，暂不作为 GitHub Release 附件发布。

## 从源码构建

需要 Windows x64 和 [.NET SDK 10.0.301](global.json)。普通 dotnet build 不会下载模型或构造试用包。可先按下列命令编译并运行不依赖模型的测试。若要运行真实翻译或制作完整包，按[完整本机版构建说明](docs/build-local-preview.md)准备 Git 忽略的模型和 Python 文件；模型来源及限制另见[可行性报告](docs/reports/local-zh-en-model-benchmark.md)。

    dotnet restore RealTimeTranslation.slnx --locked-mode
    dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
    dotnet test RealTimeTranslation.slnx --configuration Release --no-build

准备模型后，再运行 `powershell -File tools/package-local-trial.ps1` 制作完整试用包。打包脚本不负责获取或核验全部第三方分发许可。

打包脚本会生成 artifacts/RealTimeTranslation-local-zh-en-win-x64.zip 和相邻的 .sha256。模型和运行环境均不进入 Git。公开二进制发布的待办见[发布清单](docs/release-checklist.md)。

## 隐私与许可

音频、文字和本次会话历史默认不落盘；暂停和关闭会停止采集。当前默认桌面路径不向在线翻译服务发送文字。详情见[隐私说明](docs/privacy.md)。项目代码采用 [MIT 许可证](LICENSE)；模型和依赖分别适用其[第三方许可](THIRD_PARTY_NOTICES.md)。
