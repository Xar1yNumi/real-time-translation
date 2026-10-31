# Real-time Translation

[简体中文](README.zh-CN.md)

Real-time Translation is a Windows desktop project for following foreign-language videos, live streams, and online meetings with bilingual overlay subtitles. It is designed to recognize spoken Chinese, English, or Russian from **system audio** or a **microphone**, then show the source text and a translation into your chosen language. Each session uses one audio source and one target language.

> **Development status:** This repository contains an MVP in progress, not a validated release. Azure end-to-end translation, network recovery, and a continuous cloud session still need live testing. There is no release package yet.

## What the MVP is designed to do

- Detect `zh-CN`, `en-US`, or `ru-RU` between utterances and translate into Simplified Chinese, English, or Russian.
- Show provisional source text and final translations in an always-on-top, draggable, two-line-per-language subtitle bar.
- Let you pause, resume, return to settings, or close the app from the overlay.
- Use either the Windows default playback device or the default microphone. System audio captures the **whole default playback stream**, so other sounds playing on the computer may also be included.
- Keep the Azure API key in Windows Credential Manager and provide a control to delete it.

The source recognition and translation flow is implemented, but its three-language behavior and user experience have **not** been verified against a real Azure Speech resource. See the [technical feasibility report](docs/feasibility/azure-audio-report.md) and [subtitle UX checkpoint](docs/feasibility/subtitle-ux-checkpoint.md) for measured results and remaining checks.

## Requirements

- Windows x64.
- [.NET SDK 10.0.301](global.json) to build from source.
- Your own Azure AI Speech resource, HTTPS Endpoint, and API key, plus an internet connection for translation.
- A working default playback device or microphone.

The application is intended to be free and open source, but **Azure usage may incur charges** under your own subscription. Check your Azure pricing and usage limits before starting a session.

## Build and run from source

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
dotnet run --project src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --no-build
```

In the settings window:

1. Enter the Endpoint and API key from your Azure Speech resource, then select **Test connection**. The connection-test behavior still requires validation with valid and invalid live credentials.
2. Choose **System audio** or **Microphone**, then select the target language.
3. Select **Start translation**. Audio capture starts only after this action. Move the pointer over the subtitle bar to show its controls.

For a call where you want to translate the other person's voice, choose system audio and route that voice through the computer's default playback device. This MVP cannot isolate one application or speaker from other sounds in the same playback stream.

## Privacy

During an active translation session, audio is sent directly from your computer to Azure AI Speech. The project does not run its own server. The application does not intentionally save captured audio, recognized text, translations, or subtitle history. Non-sensitive preferences, including the Endpoint, are saved under the current user's local application data; the API key is stored in Windows Credential Manager. Do not put a real key in the repository, test files, logs, or issue reports.

## Project status

Local automated tests and Windows audio-capture checks exist, including a 30-minute **capture-only** loopback run. They do not establish cloud translation reliability. Before a public `v0.1.0` release, the project still needs real Azure checks for Chinese, English, and Russian; latency; disconnection and recovery; overlay interaction; and at least 30 minutes of end-to-end translation. The [implementation plan](docs/superpowers/plans/2026-10-27-real-time-translation-mvp.md) tracks the remaining work.

The intended license for the first public release is **MIT**. A `LICENSE` file and the rest of the public release documentation will be added before the repository is made public.
