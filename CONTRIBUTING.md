# Contributing

Thanks for helping improve Real-time Translation. The project is still an MVP and has not passed its Azure live-test gate. Please read the [implementation plan](docs/superpowers/plans/2026-10-27-real-time-translation-mvp.md) and [feasibility report](docs/feasibility/azure-audio-report.md) before changing runtime behavior.

## Propose a change

Open an issue describing the problem, the expected behavior, Windows version, selected audio source, target language, and reproducible steps. For feature requests, explain the user need and keep the proposal separate from the current MVP scope. A security concern should follow [SECURITY.md](SECURITY.md) instead of a public issue.

## Develop and verify

Use Windows x64 and the SDK version in `global.json`. Create a focused branch, add a test that checks the behavior, then run:

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet format RealTimeTranslation.slnx --verify-no-changes
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
```

Keep audio and Azure access behind the existing interfaces. Do not commit API keys, personal audio, transcript text, local settings, logs, or generated `artifacts/`. Include a short description of what changed, the test result, and any live checks that remain in the pull request. Only add audio fixtures you made or can redistribute under a documented license.
