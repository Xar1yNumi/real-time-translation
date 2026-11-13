# Changelog

## [v0.1.0-preview.1](https://github.com/Xar1yNumi/real-time-translation/releases/tag/v0.1.0-preview.1) — 2026-11-13

- Added local Chinese↔English translation with automatic source-language detection. The default desktop path needs no Azure subscription, account, API key, or network connection.
- Added one-at-a-time capture of default system audio or microphone, early source-text preview, and a scrollable session history of final original text and translations.
- Bundled Whisper speech recognition, Silero voice activity detection, and an OPUS-MT translation worker in the public Windows x64 preview ZIP. The official Microsoft Visual C++ Redistributable x64 is an installation prerequisite.
- Kept audio and sentence queues bounded, with recovery from a failed translation worker.
- Verified both directions with extracted-package WAV tests, default-speaker loopback to a Chinese subtitle, and a 10-minute low-duty-cycle translation test. See the [binary release report](docs/reports/2026-11-12-public-binary-release.md) and [continuity report](docs/reports/2026-11-07-local-continuity.md) for measurements and limits.

The source and Windows x64 ZIP are publicly available. The package excludes Intel and NVIDIA inference runtimes and third-party converted model archives. Full live microphone translation, real-song accuracy, physical network disconnection, and a 30-minute high-load run remain unverified. Azure cloud acceptance is deferred.
