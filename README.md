# Real-time Translation

[简体中文](README.zh-CN.md)

Windows desktop subtitles for spoken Chinese, English, and Russian. Choose one source (default speaker output or microphone) and one target language. A floating window appends recognized speech and translations throughout the session; scroll up to review earlier lines.

## Current short trial

The default app needs **no Azure account, API key, or other registration**. It recognizes speech locally with a multilingual Whisper model, then sends recognized text over HTTPS to MyMemory. An internet connection is required while translating. First use downloads and verifies a roughly 190 MB model; later launches reuse it. Audio stays on your computer. You must confirm the text-sharing notice before starting.

MyMemory allows [5,000 anonymous characters per day](https://mymemory.translated.net/doc/usagelimits.php) and [500 UTF-8 bytes per request](https://mymemory.translated.net/doc/spec.php). Service availability and translation quality vary. This trial has not passed a 30-minute service test and is not a public `v0.1.0` release. Optional Azure code remains in `RealTimeTranslation.Azure` but is absent from the default desktop package.

## Try it

On Windows x64, extract the review ZIP and run `RealTimeTranslation.Desktop.exe`. Keep the extracted files together.

1. Click **Download or verify speech model** and wait for 100%.
2. Read and check the MyMemory text-sharing box.
3. Select **System audio** for the default Windows output stream or **Microphone** for nearby speech. Select Chinese, English, or Russian as the target.
4. Click **Start translation**. Initial GPU preparation may take about 20 seconds; capture begins afterward. Move the pointer over the floating window for Pause, Return to settings, and Close. Drag the window or scroll to review this session. A new session clears the history.

While someone speaks, the bottom of the overlay shows a revisable **original-text preview**. Once the phrase and online translation finish, the original and translation are appended to the session history above. Previews stay local and do not repeatedly call MyMemory.

For calls, choose system audio if the other person's voice plays through your default output device. Other sounds in the same stream may also be captured. The app cannot isolate one application or speaker.

## Build from source

Requires Windows x64 and [.NET SDK 10.0.301](global.json):

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
dotnet run --project src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --no-build
```

The regular suite skips live service, audio-fixture, and 30-minute capture tests. See the [implementation plan](docs/superpowers/plans/2026-10-31-no-azure-online-trial.md) and [acceptance report](docs/feasibility/no-azure-trial-report.md).

## Privacy and limits

The app does not intentionally save audio, transcripts, translations, or subtitle history. Only recognized text goes to MyMemory; its [terms](https://mymemory.translated.net/terms-and-conditions) apply. Avoid confidential conversations in this trial. Pause and Close stop capture. See [privacy details](docs/privacy.md).

The app prefers Vulkan acceleration on compatible GPUs and retains a CPU runtime otherwise. In three real speaker-loopback runs on this PC, the first original-text preview appeared at about 1.6 seconds; final translations for the same five-second English clip appeared between 7.88 and 12.36 seconds. Network response time varied substantially, so this is not yet a consistently low-latency experience. A bounded queue drops older pending phrases with a warning when processing falls behind. Network requests have limited retries; quota exhaustion ends the session. Short fragments can produce unnatural translations. See the [progressive subtitle measurements](docs/reports/2026-11-02-progressive-subtitle-trial.md). The current package is for owner review; a clean Windows machine and longer service checks remain open.

This project is [MIT licensed](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md), [contributing](CONTRIBUTING.md), [security](SECURITY.md), and the [release checklist](docs/release-checklist.md).
