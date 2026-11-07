# Changelog

## Unreleased — public source preview

- Added local Chinese↔English translation with automatic source-language detection. The default desktop path needs no Azure subscription, account, API key, or network connection.
- Added one-at-a-time capture of default system audio or microphone, early source-text preview, and a scrollable session history of final original text and translations.
- Bundled Whisper speech recognition, Silero voice activity detection, and an OPUS-MT translation worker in the Windows x64 owner trial package.
- Kept audio and sentence queues bounded, with recovery from a failed translation worker.
- Verified both directions with extracted-package WAV tests, default-speaker loopback to a Chinese subtitle, and a 10-minute low-duty-cycle translation test. See [the acceptance report](docs/reports/2026-11-07-local-continuity.md) for measurements and limits.

The repository can be published as a source preview. A public binary release remains pending third-party redistribution review and a fresh package build. Azure cloud acceptance is deferred.
