# Real-time Translation

[简体中文](README.zh-CN.md)

Windows desktop subtitles for spoken Chinese and English. Choose system audio or microphone and a target language. The app detects the source language and appends original text and translations to a scrollable floating window.

## Local trial

Speech recognition, segmentation, and text translation run on the computer. No Azure, MyMemory, account, or API key is needed. The complete trial ZIP includes its models and Python runtime; users do not install Python. Once extracted, it can run without a network connection. The first launch verifies the bundled files, and initial speech engine preparation may take longer.

This stage supports Chinese and English only. If the detected source already matches the selected target, the original is shown. Other detected languages do not produce a guessed translation. Speech, video, and songs use the same path, although music and noise may impair recognition. Per-app capture, history editing, and export are later stages.

## Try it

1. Fully extract the Windows x64 trial ZIP and run RealTimeTranslation.Desktop.exe. Keep the files together.
2. Wait for the local components to show as ready. If they are incomplete, extract the whole package again.
3. Select System audio for the default speaker stream or Microphone for nearby speech, then choose Simplified Chinese or English.
4. Click Start translation. A revisable original-text preview appears first; the final original and translation are appended above. Hover over the overlay for Pause, Return to settings, and Close. Scroll to review this session; closing clears it.

For calls, choose system audio when the other person's voice plays through the default output device. Other sounds on the same stream can also be captured. The app cannot isolate one application or speaker yet.

## Verification status

The extracted trial package launched on the desktop and translated fixed English and Chinese WAV samples in both directions. A 10-minute continuous translation test passed. An English sample played through the default speaker also produced a simplified Chinese subtitle through Windows loopback. The [Task 4 report](docs/reports/2026-11-04-local-preview-vad.md) records preview and final timing; the [Task 5 report](docs/reports/2026-11-06-local-trial-task5.md) records package checks and limits. The project owner waived the full live microphone translation check for this stage. Real song accuracy and a physical network disconnect have not been measured. This package is for owner acceptance, not a public v0.1.0 release.

## Build from source

Requires Windows x64 and [.NET SDK 10.0.301](global.json). A normal build does not download models or assemble the trial package. Prepare ignored files under artifacts/models, artifacts/uv-python, and artifacts/localtranslation-venv as documented in the [model feasibility report](docs/reports/local-zh-en-model-benchmark.md), then run:

    dotnet restore RealTimeTranslation.slnx --locked-mode
    dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
    dotnet test RealTimeTranslation.slnx --configuration Release --no-build
    powershell -File tools/package-local-trial.ps1

The packaging script writes artifacts/RealTimeTranslation-local-zh-en-win-x64.zip and a sibling .sha256. Models and runtime files are excluded from Git. A public binary release still requires full third-party license review and scenario acceptance.

## Privacy and licenses

Audio, text, and session history are not saved by default. Pause and Close stop capture. The default desktop path sends no text to an online translation service. See [privacy details](docs/privacy.md). Project code is [MIT licensed](LICENSE); models and dependencies carry their own [third-party licenses](THIRD_PARTY_NOTICES.md).
