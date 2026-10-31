# No-Azure desktop UI check — 2026-11-01

## Scope and environment

- Windows 11, Ryzen 7 5800H, Realtek speaker output, Release desktop build.
- Default source: system loopback; target: Simplified Chinese.
- Public English sample: `artifacts/samples/jfk.wav` (not distributed in Git).
- Model downloaded through the settings window on first use and stored under `%LOCALAPPDATA%/RealTimeTranslation/models`.
- Downloaded model: `ggml-small-q5_1.bin`, 190,085,487 bytes; SHA-256 `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb`.

## Observations

1. With no validated model, Start was disabled. After first-use download completed, its progress reached 100%. Start remained disabled until the user checked the text-sharing consent box.
2. A fresh launch found the cached model without another download. Consent was deliberately reset for that new process; checking it enabled Start. No Azure Endpoint or API Key field appeared.
3. After Start, the floating window showed `本机识别／联网翻译中`. A public English WAV was played through the Realtek speaker. The floating window displayed the recognized phrase `what your country can do for you.` and the Chinese translation `您的国家可以为您做些什么。` The recognized phrase is a fragment of the source sample, so this check proves a real translation path, not complete transcription quality.
4. Return to Settings stopped the session and enabled Start again. Starting a new session showed blank original and translated text. Closing the floating window stopped the process.
5. The floating window was made visible in the Windows taskbar while preserving always-on-top and no-activation behavior; this also makes it locatable while the settings window is hidden.

## Automated gate

- Release solution build: succeeded with zero warnings and zero errors.
- Desktop target tests: 20 passed, zero failed.
- Full solution tests: 174 total, 167 passed, seven conditional live/Azure tests skipped, zero failed.
- `dotnet format --verify-no-changes`: passed.

## Limits of this check

- Exact phrase-end-to-final-subtitle latency was not measured by a synchronized clock.
- The UI check covered one English-to-Chinese phrase and system loopback. Chinese/Russian input, microphone, network loss, quota response, and extracted-package launch remain in NA7.
- This short trial does not establish 30-minute cloud or anonymous-service reliability.
