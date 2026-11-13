# Public preview release checklist

Status: **Chinese–English local MVP preview**. Azure is optional legacy source code and is not part of the default desktop application. The owner authorized public source and binary publication on 2026-11-07.

## Repository

- [x] Project source has an MIT LICENSE, bilingual README, contribution guide, security policy, privacy statement, and third-party notices.
- [x] Source and test fixtures contain no model weights, personal audio, credentials, generated packages, or local settings. artifacts/ is ignored by Git.
- [x] Default desktop path uses bundled local speech recognition and Chinese–English translation, with no account, API key, or network service.
- [x] Merge the current branch into main; PR #1 passed both GitHub Actions checks and was squash-merged on 2026-11-07.
- [x] Make the GitHub repository public on 2026-11-07. GitHub settings and the repository connector both report `public`.
- [x] Verify an unauthenticated request to the repository URL returns HTTP 200. GitHub settings show `main` as the default branch.

## Binary package

- [x] Independently build a pinned CTranslate2 4.8.4 CPU wheel with OpenBLAS/Ruy and without Intel or NVIDIA inference runtimes. See [release report](reports/2026-11-12-public-binary-release.md).
- [x] Independently convert both OPUS-MT directions from hash-verified Helsinki-NLP source revisions; use their upstream license labels and include model credits.
- [x] Package and extract a Windows x64 candidate; scan native files and verify ZIP SHA-256. The old Intel-based private ZIP is excluded.
- [x] Run English and Chinese WAV through the freshly extracted candidate; both directions produce subtitles. Run 207 direct non-live .NET tests with no failures.
- [x] Run the new CPU package for 10 minutes with ordered subtitles, bounded queues and bounded memory growth; see [release report](reports/2026-11-12-public-binary-release.md).
- [x] Rebuild with the threaded OpenBLAS wheel, then pass fresh-extraction checks, both WAV directions, the 250 ms per-direction P95 gate, the 10-minute stability test, and executable launch. The single-threaded `r6` ZIP is rejected; see the [release report](reports/2026-11-12-public-binary-release.md).
- [ ] Merge the final source commit, build the desktop application and release ZIP from it using the audited CPU wheel, then upload ZIP plus checksum to a GitHub Release and verify the public download.

## Limits to disclose

- Only Chinese↔English translation is supported. Recognition may be inaccurate with music, background noise, or multiple speakers.
- System audio is the entire default playback stream; individual applications and speakers cannot be isolated.
- The 10-minute tests use about five seconds of speech per minute. A 30-minute high-duty-cycle or WPF-window soak test has not been run.
- A physical network disconnect was not measured. Full live microphone translation was waived by the owner; it is not claimed as tested.

The [earlier binary license audit](reports/2026-11-07-binary-license-audit.md) documents a private candidate that bundled Intel OpenMP. It is retained for historical context and is not a public release asset.
