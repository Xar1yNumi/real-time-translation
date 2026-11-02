# Short-trial review and public release checklist

Status: **owner review package, not a public `v0.1.0` release**. The default desktop app runs without Azure credentials. Anonymous MyMemory service limits and phrase-boundary delays constrain this short trial. The old Azure cloud acceptance remains deferred until the owner decides whether to use Azure.

## Review package

- [x] The default desktop flow has no Azure credential gate; model validation and text-sharing consent gate Start.
- [x] English, Chinese, and Russian audio have produced recognized source text and real online translations on this PC. See the [trial report](feasibility/no-azure-trial-report.md).
- [x] Six text translation directions returned live HTTPS translations without credentials.
- [x] The no-Azure desktop publish does not contain Azure Speech SDK binaries.
- [x] Run locked restore, format, Release build, and the complete automated suite after the subtitle-history and Vulkan revisions: 173 passed, 11 conditional skips, zero failures.
- [x] Produce the self-contained `win-x64` ZIP and SHA-256 in desktop `artifacts/`, extract it, launch the extracted app, and observe a real English-to-Chinese floating subtitle.
- [x] Verify every relative link included in the archive and scan it for credentials, user audio, model weights, logs, and sample fixtures.
- [x] Confirm microphone signal and a real translated sentence from that source. Prompted Chinese speech produced an English subtitle; the latter clause was inaccurate, so quality acceptance remains open.
- [x] Confirm recovery in the same app session after an app-scoped proxy returned two connection failures and then forwarded the next request; a real Chinese subtitle appeared.
- [x] Confirm the visible network warning during four consecutive proxy failures, then restore forwarding and observe a new real translation and cleared warning without restarting the app.
- [x] Retain each finalized subtitle pair for the whole current session, with scrolling and an independent partial line; verify reset, duplicates, and multi-line text in automated tests.
- [x] Benchmark GPU recognition on English, Chinese, and Russian clips; run all three opt-in online audio chains after preparation. See the revision in the [trial report](feasibility/no-azure-trial-report.md).
- [x] Build the revised self-contained ZIP, verify its SHA-256, extract it, compare the executable hash, confirm the Vulkan runtime is present, and check all bundled relative links. The new archive contains no Azure SDK binaries, model weights, audio fixtures, or logs.
- [ ] Inspect the revised floating window visually in a launched review package. The preview launch encountered an application approval timeout.
- [x] Add original-text previews before phrase end without sending them to MyMemory; keep a bounded latest-only preview task and preserve final subtitle history when earlier translations arrive late.
- [x] Rebuild Release with 0 warnings/errors; run 193 regular tests: 179 passed, 14 conditional skips, 0 failures; verify C# format.
- [x] Run timed English and Chinese WAV tests and a real default-speaker loopback test. Preview appeared around 1.6 seconds; online final timing varied. See the [progressive trial report](reports/2026-11-02-progressive-subtitle-trial.md).
- [x] Build and extract `artifacts/RealTimeTranslation-noazure-progressive-20261102-win-x64.zip`; archive and executable hashes match, and no Azure SDK, audio sample, model weight, or log file is bundled.
- [ ] Visually inspect the new progressive review package. Launch showed the application's single-instance notice because another instance was already running; no current overlay rendering was observed.

## Public `v0.1.0` decision

- [ ] Owner reviews actual translation accuracy and phrase-end latency, especially Russian fragments and the first-start preparation delay.
- [ ] Test on a separate clean Windows x64 machine.
- [ ] Determine whether a service with anonymous daily limits is acceptable for the intended use and whether Azure should return as an optional provider.
- [ ] Measure sustained end-to-end runtime and service reliability; the existing 30-minute capture-only test does not establish translation stability.
- [ ] Review all third-party licenses, model terms, copyright holder in [LICENSE](../LICENSE), and [notices](../THIRD_PARTY_NOTICES.md).
- [ ] Obtain explicit owner approval before a public GitHub Release, `v0.1.0` tag, or public repository change.
