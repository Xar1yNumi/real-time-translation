# Short-trial review and public release checklist

Status: **owner review package, not a public `v0.1.0` release**. The default desktop app runs without Azure credentials. Anonymous MyMemory service limits and measured CPU latency constrain this short trial. The old Azure cloud acceptance remains deferred until the owner decides whether to use Azure.

## Review package

- [x] The default desktop flow has no Azure credential gate; model validation and text-sharing consent gate Start.
- [x] English, Chinese, and Russian audio have produced recognized source text and real online translations on this PC. See the [trial report](feasibility/no-azure-trial-report.md).
- [x] Six text translation directions returned live HTTPS translations without credentials.
- [x] The no-Azure desktop publish does not contain Azure Speech SDK binaries.
- [x] Run locked restore, format, Release build, and the complete automated suite after the Azure adapter split and network-warning fix: 170 passed, 11 conditional skips, zero failures.
- [x] Produce the self-contained `win-x64` ZIP and SHA-256 in desktop `artifacts/`, extract it, launch the extracted app, and observe a real English-to-Chinese floating subtitle.
- [x] Verify every relative link included in the archive and scan it for credentials, user audio, model weights, logs, and sample fixtures.
- [ ] Confirm microphone signal and a real translated sentence from that source. The Realtek microphone session started, but its peak meter stayed at zero during speaker playback.
- [ ] Confirm offline warning and recovery using an app-scoped network failure without disrupting the computer's other connections. The sandbox blocked a loopback proxy test before it reached the app.

## Public `v0.1.0` decision

- [ ] Owner reviews actual translation accuracy and latency, especially Russian fragments and CPU delay.
- [ ] Test on a separate clean Windows x64 machine.
- [ ] Determine whether a service with anonymous daily limits is acceptable for the intended use and whether Azure should return as an optional provider.
- [ ] Measure sustained end-to-end runtime and service reliability; the existing 30-minute capture-only test does not establish translation stability.
- [ ] Review all third-party licenses, model terms, copyright holder in [LICENSE](../LICENSE), and [notices](../THIRD_PARTY_NOTICES.md).
- [ ] Obtain explicit owner approval before a public GitHub Release, `v0.1.0` tag, or public repository change.
