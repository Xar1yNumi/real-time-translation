# Public preview release checklist

Status: **Chinese–English local MVP preview**. This is the current release baseline; Azure is optional legacy source code and is not part of the default desktop application. The owner authorized publication of the current project on 2026-11-07.

## Repository

- [x] Project source has an MIT LICENSE, bilingual README, contribution guide, security policy, privacy statement, and third-party notices.
- [x] Source and test fixtures contain no model weights, personal audio, credentials, generated packages, or local settings. artifacts/ is ignored by Git.
- [x] Default desktop path uses bundled local speech recognition and Chinese–English translation, with no account, API key, or network service.
- [ ] Merge the current branch into main and make the GitHub repository public.
- [ ] Verify the public repository and its default branch from an unauthenticated view.

## Binary package

- [x] Desktop ZIP was produced and extracted on Windows x64; both translation directions and a default-speaker loopback path produced subtitles. See [Task 5](reports/2026-11-06-local-trial-task5.md).
- [x] A 10-minute low-duty-cycle test of the extracted package passed with ordered subtitles and bounded queues. See [continuity results](reports/2026-11-07-local-continuity.md).
- [ ] Finish the redistribution audit for bundled CPython packages, native DLLs, and converted models. The upstream English-to-Chinese model declares Apache-2.0 at the pinned revision; local conversion metadata currently says CC-BY-4.0 and must be corrected before distributing a binary.
- [ ] Build a fresh package from the final public commit, verify SHA-256, inspect its contents, and publish it as a GitHub Release asset only after the license audit is complete.

## Limits to disclose

- Only Chinese↔English translation is supported. Recognition may be inaccurate with music, background noise, or multiple speakers.
- System audio is the entire default playback stream; individual applications and speakers cannot be isolated.
- The 10-minute test had about five seconds of speech per minute. A 30-minute high-duty-cycle or WPF-window soak test has not been run.
- A physical network disconnect was not measured. Full live microphone translation was waived by the owner; it is not claimed as tested.

This checklist distinguishes a public **source preview** from a public **binary release**. Do not publish the existing owner trial ZIP as a release asset while its redistribution review is open.
