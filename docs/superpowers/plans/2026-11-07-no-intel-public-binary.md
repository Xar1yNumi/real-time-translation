# No-Intel Public Binary Implementation Plan

> **For agentic workers:** Execute these tasks in order with one verifiable result per task.

**Goal:** Publish a working, self-contained Windows x64 Chinese–English subtitle ZIP without bundled Intel runtime files or third-party converted models.

**Architecture:** A Windows CI job compiles pinned CTranslate2 with OpenBLAS/Ruy and no OpenMP. The local packaging script combines this audited wheel, independently converted pinned OPUS-MT models, and the existing WPF application.

**Tech Stack:** .NET 10/WPF, Python 3.11, CTranslate2 4.8.4, CMake/MSVC, OpenBLAS, SentencePiece, GitHub Actions.

## Global Constraints

- Keep existing UI/audio behavior and Chinese–English scope.
- No Azure, API key, account, mandatory online service, Intel or NVIDIA runtime in the release ZIP.
- Do not publish any binary until provenance, licenses, fresh extraction, live translation, latency and stability gates pass.

---

### Task 1: Build independent CPU inference wheel

**Files:** `.github/workflows/build-cpu-translation.yml`, `tools/build-cpu-translation.ps1`.

- [x] Pin the CTranslate2 v4.8.4 source commit and build flags in the Windows CI workflow. Verify the OpenBLAS threaded backend, then pin the audited wheel and native DLL hashes in the packaging script.
- [x] Build and install OpenBLAS, compile the C++ library and Python wheel; upload an internal CI artifact.
- [x] Inspect wheel DLLs/imports for Intel/NVIDIA dependencies and run a two-direction sample on the owner PC.
- [x] Record source hashes, dependency notices, and translation latency. Commit only after a passing build.

### Task 2: Convert pinned upstream models

**Files:** `tools/convert-opus-models.ps1`, `tools/LocalTranslation.Probe/model-manifest.json`, `third_party_licenses/OPUS-MT-model-credits.txt`.

- [x] Download both upstream revisions directly and verify each source file hash.
- [x] Convert both to CTranslate2 format, retain tokenizer files and accurate source license metadata.
- [x] Run the fixed eight-sentence probes and compare meaning, direction, and speed with the current private candidate.
- [x] Record every packaged model hash and conversion command; commit after tests pass.

### Task 3: Produce audited complete package

**Files:** `tools/package-local-trial.ps1`, `THIRD_PARTY_NOTICES.md`, `README.md`, `README.zh-CN.md`, `docs/release-checklist.md`.

- [x] Package only the new wheel, model files, Python runtime and WPF app; reject Intel/NVIDIA DLLs and stale model hashes.
- [x] Include OpenBLAS/Ruy and other applicable license texts and provenance notes.
- [x] Run locked restore, Release build, direct test runners, extracted-ZIP two-direction live test, latency probe and 10-minute stability test.
- [x] Verify ZIP SHA-256 and contents, then commit and push for CI review.

### Task 4: Publish

**Files:** `docs/reports/2026-11-07-public-binary-release.md`, release notes on GitHub.

- [ ] Confirm CI success and merge the source PR.
- [ ] Rebuild the desktop application and complete ZIP from the merged commit using the previously audited CPU wheel, repeat package checks, and upload ZIP plus checksum to a GitHub Release. A newly compiled wheel requires its own hash and import audit before changing the release pin.
- [ ] Verify public download availability and matching checksum; update documentation and close the binary release checklist.
