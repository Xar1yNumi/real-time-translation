# Real-time Translation

[简体中文](README.zh-CN.md)

Windows desktop subtitles for spoken Chinese and English. Choose system audio or microphone and a target language. The app detects the source language and appends original text and translations to a scrollable floating window.

## Windows x64 preview

Speech recognition, segmentation, and text translation run on the computer. No Azure, MyMemory, account, or API key is needed. The trial ZIP includes its models and Python runtime; users do not install Python. **Install the [official Microsoft Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe) first, unless a recent x64 version is already installed.** The ZIP does not include the full VC++ Redistributable; Python carries its own `vcruntime140` files. After that prerequisite is installed, translation does not require a network connection. The first launch verifies the bundled files, and initial speech engine preparation may take longer.

This stage supports Chinese and English only. If the detected source already matches the selected target, the original is shown. Other detected languages do not produce a guessed translation. Speech, video, and songs use the same path, although music and noise may impair recognition. Translation can paraphrase or omit part of a sentence. Per-app capture, history editing, and export are later stages.

## Try it

Download the complete Windows x64 ZIP and its checksum from [v0.1.0 Preview 1](https://github.com/Xar1yNumi/real-time-translation/releases/tag/v0.1.0-preview.1), or follow “Build from source” below. The ZIP includes the local models and Python runtime.

1. Install the [Microsoft Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe) from Microsoft if it is not already present.
2. Fully extract the Windows x64 trial ZIP and run RealTimeTranslation.Desktop.exe. Keep the files together. The ZIP also contains `START-HERE.txt`.
3. Wait for the local components to show as ready. If they are incomplete, extract the whole package again. If the VC++ runtime is missing, the app displays the official download link.
4. Select System audio for the default speaker stream or Microphone for nearby speech, then choose Simplified Chinese or English.
5. Click Start translation. A revisable original-text preview appears first; the final original and translation are appended above. Hover over the overlay for Pause, Return to settings, and Close. Scroll to review this session; closing clears it.

For calls, choose system audio when the other person's voice plays through the default output device. Other sounds on the same stream can also be captured. The app cannot isolate one application or speaker yet.

## Verification status

The [public binary release report](docs/reports/2026-11-12-public-binary-release.md) records the CPU build, model provenance, package checks and live test results. The [earlier license audit](docs/reports/2026-11-07-binary-license-audit.md) describes a different, private candidate that is not a release asset.

The new CPU package passed a fresh extracted-ZIP English and Chinese WAV test, including speech recognition and translated subtitles, plus a 10-minute low-duty-cycle continuity test. Earlier default-speaker loopback evidence is in the [continuity report](docs/reports/2026-11-07-local-continuity.md). The project owner waived full live microphone testing; real song accuracy and physical network disconnection have not been measured.

## Build from source

Requires Windows x64 and [.NET SDK 10.0.301](global.json). A normal build does not download models or assemble the trial package. The following commands build the source and run tests that do not require local models. For live translation or a complete package, follow the [complete local build guide](docs/build-local-preview.md) to prepare ignored model and Python files; the [model feasibility report](docs/reports/local-zh-en-model-benchmark.md) records sources and limitations.

    dotnet restore RealTimeTranslation.slnx --locked-mode
    dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
    dotnet test RealTimeTranslation.slnx --configuration Release --no-build

After model preparation, run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package-local-trial.ps1` to assemble a complete trial. The script checks pinned model and native-runtime hashes, but does not download its inputs.

The packaging script writes `artifacts/RealTimeTranslation-local-zh-en-win-x64.zip` and a sibling `.sha256`. Models and runtime files are excluded from Git. See the [release checklist](docs/release-checklist.md) for publication status.

## Privacy and licenses

Audio, text, and session history are not saved by default. Pause and Close stop capture. The default desktop path sends no text to an online translation service. See [privacy details](docs/privacy.md). Project code is [MIT licensed](LICENSE); models and dependencies carry their own [third-party licenses](THIRD_PARTY_NOTICES.md).
