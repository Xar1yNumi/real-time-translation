# Progressive Subtitles Implementation Plan

> **For agentic workers:** Use inline execution task by task. Each task has a red/green test cycle, Release build/test, review, and its own Git commit.

**Goal:** Show updated original speech before phrase end while preserving an accumulating, correctly ordered translation history.

**Architecture:** `PcmUtteranceSegmenter` emits bounded preview snapshots and final utterances with stable IDs. `NoAzureTranslationSession` recognizes previews independently of online translation and prioritizes final speech. `TranslationCoordinator` passes completed utterances separately so a late final can append history without replacing a newer preview.

**Tech Stack:** .NET 10, C#, WPF, NAudio, Whisper.net, MyMemory, xUnit v3.

## Global Constraints

- Keep one audio source per session and existing `zh`/`en`/`ru` recognition.
- Only finalized text is sent to MyMemory; preview stays local and in memory.
- Preserve bounded queues, cancellation, session identity, and current MIT ownership.
- Do not claim live latency from rapid PCM injection.

---

### Task 1: Stable progressive PCM snapshots

**Files:** `src/RealTimeTranslation.Infrastructure/LocalSpeech/PcmUtteranceSegmenter.cs`, `tests/RealTimeTranslation.Infrastructure.Tests/PcmUtteranceSegmenterTests.cs`.

**Interface:** `PushProgress(ReadOnlySpan<byte>)` returns ordered `PcmSpeechProgress` entries: `UtteranceNumber`, `Pcm`, `IsFinal`. Existing `Push` remains a final-only wrapper for compatibility.

- [x] Add a failing test: 2 seconds of voiced PCM without silence produces a nonfinal snapshot; further voice produces an updated snapshot with the same number; 600 ms silence produces one final with that number. Run the segmenter tests and observe the expected failure.
- [x] Implement preview cadence near 1 second of voiced input, with an 0.8 second minimum and no more than one active utterance snapshot retained by the segmenter. Preserve the 8 second buffer cap and short-noise rejection.
- [x] Run segmenter tests, Release build, solution tests, and `git diff --check`. Commit this task alone.

### Task 2: Preview recognition alongside final translation

**Files:** `src/RealTimeTranslation.Infrastructure/Translation/NoAzureTranslationSession.cs`, `tests/RealTimeTranslation.Infrastructure.Tests/NoAzureTranslationSessionTests.cs`.

**Interface:** Preview and final progress for one segment share `UtteranceId` and `Sequence`. `UpdateReceived` emits `PartialTranscriptUpdate` from a latest-only preview worker and `FinalUtteranceUpdate` from the existing final queue. Preview must not call `IMyMemoryTranslator`.

- [x] Add failing tests for preview before silence, same ID on final, and next preview while a previous translation is held. Confirm each fails for the missing behavior. The slow-recognizer latest-only case remains covered by the implementation's cancellation/revision check but lacks a separate focused test.
- [x] Add a cancellable latest-only preview worker; cancel obsolete snapshots and prioritize final recognition through the existing transcriber gate. Keep the final queue at two and preserve its warning behavior.
- [x] Run focused tests, Release build, solution tests, and `git diff --check`. Commit this task alone.

### Task 3: Late final arrives without replacing newer preview

**Files:** `src/RealTimeTranslation.Core/TranslationCoordinator.cs`, `src/RealTimeTranslation.Desktop/ViewModels/SubtitleViewModel.cs`, `tests/RealTimeTranslation.Core.Tests/TranslationCoordinatorTests.cs`, `tests/RealTimeTranslation.Desktop.Tests/SubtitleViewModelTests.cs`.

**Interface:** `SubtitleChangedEventArgs` optionally carries the completed `FinalUtteranceUpdate`. The current `SubtitleState` still selects the newest sequence; the ViewModel appends the completed line by ID even when its sequence is older.

- [x] Add failing tests: after partial sequence 2, final sequence 1 leaves current preview untouched and adds sequence 1 to history exactly once; a new session clears both.
- [x] Implement the event payload and ViewModel history update; keep the existing same-language notice and session cleanup behavior.
- [x] Run focused tests, Release build, solution tests, `dotnet format --verify-no-changes`, and `git diff --check`. Commit this task alone.

### Task 4: Measured trial package and documentation

**Files:** `docs/feasibility/no-azure-trial-report.md`, `README.md`, `README.zh-CN.md`, `docs/release-checklist.md`, test or benchmark files only where they provide reproducible measurements.

- [x] Run the prepared English and Chinese WAV through the updated path and record preview/final timing and inference conditions. Run a real system-output playback check if the current desktop permits it.
- [x] Publish a review ZIP only after Release build and tests pass. State actual measurements and remaining limits; do not call it a public release.
- [x] Commit docs/package metadata separately and push the working branch to GitHub.
