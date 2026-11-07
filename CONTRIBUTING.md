# Contributing

Thanks for helping improve Real-time Translation. The current default desktop path is a local Chinese–English preview. Please read the [Chinese README](README.zh-CN.md), [privacy statement](docs/privacy.md), and [current release checklist](docs/release-checklist.md) before changing runtime behavior.

Open an issue with the expected and actual behavior, Windows version, selected audio source, target language, and reproducible steps. Keep private audio, transcripts, account details, and logs out of public issues. Follow [SECURITY.md](SECURITY.md) for vulnerability reports.

For a code change, use a focused branch and run on Windows x64 with the SDK in global.json:

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet format RealTimeTranslation.slnx --verify-no-changes --no-restore
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
```

Tests that require local models, a bundled package, audio hardware, or external services are conditional and must be reported separately. Do not commit API keys, personal recordings, transcript text, local settings, logs, generated artifacts/, or model weights. Add audio fixtures only when redistribution rights are documented. A pull request should state what changed, test results, and live checks that remain open.
