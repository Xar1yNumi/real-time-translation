# No-Azure Online Translation Trial Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a Windows trial that shows real Chinese, English and Russian translated subtitles without Azure credentials or another account, using local speech recognition and anonymous online text translation.

**Architecture:** Keep the existing WPF window, single audio source, bounded PCM queue, coordinator and subtitle state. Add a bounded local utterance processor using Whisper.net and a MyMemory HTTP client. Wire these behind `ITranslationProvider`; change the default UI to model download, privacy consent and no-key start. Keep Azure code out of the active path until the owner decides whether to retain it.

**Tech Stack:** .NET 10, WPF, NAudio, Whisper.net 1.9.1 CPU runtime, pinned multilingual `ggml-small-q5_1.bin`, `HttpClient`, xUnit v3.

## Global Constraints

- Work in the desktop checkout `C:\Users\ChenX\Desktop\Real-time translation` on `codex/noazure-short-trial`; the owner asked to inspect this folder directly.
- The [approved design](../specs/2026-10-31-no-azure-online-trial-design.md) controls privacy, six language directions, first-use model download and short-trial limits.
- Never require Azure Endpoint, API Key or an account for this trial. Do not send audio to MyMemory; send only confirmed text after explicit user consent.
- MyMemory anonymous use is capped at 5,000 characters per day and each `q` is limited to 500 UTF-8 bytes. Do not imply unlimited use or 30-minute acceptance.
- Never log audio, transcript, translation, full request URL or credentials. Never commit model weights, user settings or generated audio.
- Each task: failing test, minimal implementation, locked restore if dependencies changed, Release build, targeted and full tests, diff check, independent commit. Keep `main` and the old Azure trial package untouched until a real no-Azure end-to-end check passes.

## File map

| Responsibility | Files |
| --- | --- |
| Model download and validation | `src/RealTimeTranslation.Infrastructure/LocalSpeech/WhisperModelStore.cs`, matching infrastructure tests |
| Speech segmentation and WAV framing | `src/RealTimeTranslation.Infrastructure/LocalSpeech/PcmUtteranceSegmenter.cs`, `PcmWaveWriter.cs`, matching tests |
| Whisper adapter | `src/RealTimeTranslation.Infrastructure/LocalSpeech/WhisperTranscriber.cs`, integration tests |
| Anonymous translation | `src/RealTimeTranslation.Infrastructure/AnonymousTranslation/MyMemoryTranslator.cs`, matching tests |
| Provider/session bridge | `src/RealTimeTranslation.Infrastructure/AnonymousTranslation/NoAzureTranslationProvider.cs`, `NoAzureTranslationSession.cs`, matching tests |
| User-facing default | `src/RealTimeTranslation.Desktop/App.xaml.cs`, `SettingsWindow.xaml`, `ViewModels/SettingsViewModel.cs`, desktop tests |
| Trial distribution and evidence | `README.md`, `README.zh-CN.md`, `docs/privacy.md`, `docs/release-checklist.md`, `docs/feasibility/no-azure-trial-report.md`, `.github/workflows/release.yml` |

---

### Task NA1: Verified speech model acquisition

**Interfaces:** `IWhisperModelStore.EnsureAvailableAsync(IProgress<double>?, CancellationToken)` returns a validated absolute model path. The store takes `HttpClient`, a model directory and an internal `WhisperModelDescriptor(Uri Source, long Length, string Sha256)` so tests use a temporary directory, a tiny known payload and an injected handler; production uses the pinned descriptor in the design.

- [x] Write tests in `tests/RealTimeTranslation.Infrastructure.Tests/WhisperModelStoreTests.cs` proving: a matching cached model causes zero HTTP calls; an interrupted download leaves no final model; wrong length or SHA-256 is rejected; a valid response is atomically installed.
- [x] Run `dotnet test tests/RealTimeTranslation.Infrastructure.Tests --configuration Release --filter WhisperModelStoreTests` and verify the new tests fail because the store does not exist.
- [x] Add centrally pinned `Whisper.net` and `Whisper.net.Runtime` version `1.9.1`, and implement the store with the model URL, length and SHA-256 from the design. Store weights under `%LOCALAPPDATA%/RealTimeTranslation/models`; use a temporary file and `File.Move` only after hashing.
- [x] Run locked restore, Release build, targeted tests and full suite; review that no model file is staged, then commit `feat: verify and cache local speech model`.

### Task NA2: Bounded utterance segmentation

**Interfaces:** `PcmUtteranceSegmenter.Push(ReadOnlySpan<byte>)` returns zero or more complete PCM16 mono utterances; `Flush()` emits a final non-silent utterance. `PcmWaveWriter.Wrap16kMono(ReadOnlySpan<byte>)` returns a valid RIFF/WAVE byte array.

- [x] Write tests in `PcmUtteranceSegmenterTests.cs` and `PcmWaveWriterTests.cs` using synthetic PCM: silence emits nothing, speech plus 600 ms silence emits exactly one utterance, >8 seconds forces a split, `Flush()` clears pending PCM, WAV lengths and `fmt` fields match 16 kHz/16-bit/mono.
- [x] Run targeted tests and observe the expected missing-type failures.
- [x] Implement a 200 ms pre-roll, 400 ms minimum voiced portion, 600 ms trailing silence and 8-second maximum. Keep the segmenter's PCM buffer bounded; NA5 owns the two-item pending-utterance queue and its observable overrun result.
- [x] Run Release build, targeted and full tests; commit `feat: segment bounded local speech utterances`.

### Task NA3: Local multilingual transcription

**Interfaces:** `IWhisperTranscriber.TranscribeAsync(ReadOnlyMemory<byte> pcm16Mono, CancellationToken)` returns `TranscribedUtterance(Text, LanguageCode)` or a classified unsupported/empty result. `WhisperTranscriber` loads only the validated path from NA1 and uses `WhisperFactory` with automatic language detection.

- [x] Add tests for empty input, unsupported detected language and cancellation using an injected internal inference adapter; add a real English WAV integration test with a reproducible licensed fixture, guarded by the downloaded model's presence.
- [x] Run tests to see the missing adapter behavior; implement a single serialized Whisper processor so concurrent audio cannot corrupt native state. Convert each segment with NA2's WAV writer; allow only `zh`, `en`, `ru`.
- [x] Run the real English fixture on this PC and record original text, detected language and elapsed time. If the CPU runtime cannot keep pace with short phrases, measure before selecting an accelerated runtime.
- [x] Run Release build and full suite; commit `feat: transcribe local multilingual speech`.

### Task NA4: Anonymous HTTPS translation client

**Interfaces:** `IMyMemoryTranslator.TranslateAsync(string text, string source, string target, CancellationToken)` returns nonempty translated text or throws a classified `AppFailureException`. The concrete client takes an injected `HttpClient` for deterministic tests.

- [x] Write `MyMemoryTranslatorTests.cs` with a fake HTTP handler for six `zh/en/ru` directions, UTF-8 encoding, 500-byte rejection, empty/invalid JSON, HTTP 429, service quota response, timeout and cancellation. Verify same-language text is returned without network access.
- [x] Run targeted tests to verify RED; implement only the documented `get` endpoint, a finite timeout and safe error messages. Never include `q` or the full URL in exceptions or logs.
- [x] Run six harmless live text samples as a separate manual check; record actual status and elapsed time without claiming long-term reliability.
- [x] Run Release build, targeted and full tests; commit `feat: translate short text with anonymous service`.

### Task NA5: Real audio-to-subtitle session

**Interfaces:** `NoAzureTranslationProvider : ITranslationProvider` and `NoAzureTranslationSession : ITranslationSession`. The session consumes existing 16 kHz PCM, sends bounded utterances to NA3, emits `PartialTranscriptUpdate` with recognized text, then emits one `FinalUtteranceUpdate` with NA4's translation and the same `UtteranceId`/`Sequence`.

- [ ] Write lifecycle tests for start/write/stop/dispose, duplicate and stale results, two-item backlog, offline/network errors, quota exhaustion, pause/resume and no Azure calls, using fake transcriber/translator.
- [ ] Run tests to verify RED; implement a single bounded worker, cancellation on stop and finite waits so capture callbacks never block on model or network work. Map error messages in `AppError` without exposing Azure-only wording in this path.
- [ ] Run Release build and full tests, inspect memory/backlog under sustained synthetic audio, then commit `feat: stream local speech to online subtitles`.

### Task NA6: No-key desktop flow and privacy consent

**Interfaces:** `SettingsViewModel` exposes model state/progress, `DownloadModelCommand`, `HasTextSharingConsent`, and `CanStart` based on valid model, consent, selected source and target. `App.xaml.cs` constructs the no-Azure provider. Azure fields are absent from the short-trial settings view.

- [ ] Add desktop tests proving Start disabled before model and consent, enabled afterward without Endpoint/Key, Start calls the new provider, model/download/network errors are visible, and stop restores settings without retaining old subtitle text.
- [ ] Run targeted desktop tests to verify RED; implement WPF copy: “音频仅在本机识别；确认的文字会发送到 MyMemory；匿名额度每天 5,000 字符”. Show download progress and a clear quota/offline state.
- [ ] Run Release build and full tests; launch from the desktop build and visually verify controls and closed-window cleanup; commit `feat: start no-Azure translation from desktop`.

### Task NA7: Short-trial package and actual acceptance

**Interfaces:** a self-contained `win-x64` ZIP and `.sha256` in desktop `artifacts/`, plus a report whose evidence is separate from automated tests.

- [ ] Use consenting/reusable English, Chinese and Russian audio fixtures; record model/version/hash, recognition text and language for each. Exercise all six directions over HTTPS, system loopback and microphone where a speaker is available. Measure phrase-end to final-subtitle latency and CPU/RAM on this PC.
- [ ] Verify network loss, anonymous quota handling, stop/resume and clean exit. A live check is incomplete if a real sentence does not produce a real translation in the UI.
- [ ] Update bilingual READMEs, privacy notes, release checklist and package workflow to describe the new default and the 5,000-character limit. Keep public GitHub Release/tag disabled; include required third-party notices before distributing binaries beyond owner review.
- [ ] Run locked restore, format check, Release build, full tests, package extraction, link scan, SHA-256 check and fresh launch from the extracted desktop package. Commit `build: deliver no-Azure short trial` and push the verified branch to GitHub for review.

## Stop conditions

- If the anonymous service stops accepting account-free requests, exceeds its documented quota during acceptance or cannot produce usable Chinese/Russian translations, record the evidence and return to design selection. Do not silently substitute an undocumented website endpoint.
- If the model cannot be downloaded, validated or run on the owner's PC, report the exact failure and benchmark before changing model/runtime.
- Do not mark the project fully complete, create `v0.1.0`, or claim 30-minute stability on the strength of this short trial.
