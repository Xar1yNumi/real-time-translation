# v0.1.0 release checklist

Status: **not release ready**. The owner deferred Azure live validation on 2026-10-30 to allow MVP development to continue. This does not count as a pass and does not authorize a tag or public release.

Preflight on 2026-10-31: locked restore, format verification, Release build, and the automated suite passed (103 passed, 4 conditionally skipped). A self-contained `win-x64` folder with the Azure Speech native DLLs was published and zipped locally; the 68,311,154-byte archive had SHA-256 `5120FDC5B24963E614E0E1F712419C5DF216C0B8E88845FB185F6D3805D03CDB`. The archive is ignored by Git and is not a release. Launching the published archive on a separate Windows machine and the live acceptance cases below remain open.

## Reproducible build

- [x] `dotnet restore RealTimeTranslation.slnx --locked-mode`
- [x] `dotnet format RealTimeTranslation.slnx --verify-no-changes`
- [x] `dotnet build RealTimeTranslation.slnx --configuration Release --no-restore`
- [x] `dotnet test RealTimeTranslation.slnx --configuration Release --no-build` (4 live/manual checks skipped)
- [x] Publish a self-contained `win-x64` folder, zip it, and record SHA-256.
- [ ] Inspect the archive for Azure Speech native files and launch it on Windows x64.

## Live acceptance

- [ ] Test a valid and an invalid Azure Speech Endpoint and API key without exposing either key.
- [ ] Verify Chinese, English, and Russian recognition and all intended target directions with reproducible audio or consenting speakers.
- [ ] Measure source speech end to final subtitle latency, including average and P95; compare with the design target.
- [ ] Test real network loss, 1/2/4-second retries, recovery, and final failure.
- [ ] Test audio-device removal, pause/resume, credential deletion, and clean exit.
- [ ] Run at least 30 minutes of **end-to-end Azure translation**, checking subtitle output and resource use. The completed 30-minute loopback capture-only test does not satisfy this item.
- [ ] Review the [feasibility report](feasibility/azure-audio-report.md) and record actual results for every open live-test row.

## Repository and publication

- [ ] Review English and Chinese README files for matching Azure cost, privacy, capabilities, and limitations.
- [ ] Confirm the copyright holder in [LICENSE](../LICENSE) with the repository owner.
- [ ] Verify links, issue templates, contribution guide, security route, and screenshots or an explicit screenshot gap.
- [ ] Scan tracked files and the release archive for secrets, personal data, logs, and generated test artifacts.
- [ ] Move `CHANGELOG.md` from `Unreleased` to `0.1.0` only after all release checks pass.
- [ ] Obtain the owner's explicit authorization before creating or pushing `v0.1.0` or making the repository public.
