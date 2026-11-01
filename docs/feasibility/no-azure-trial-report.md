# No-Azure short-trial acceptance report — 2026-11-01

## Environment and fixtures

- Windows x64; AMD Ryzen 7 5800H; default Realtek speaker output; Whisper.net 1.9.1 CPU runtime.
- Local multilingual model `ggml-small-q5_1.bin`, 190,085,487 bytes, SHA-256 `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb`. The app downloaded and validated it on first use, then reused it on a fresh launch.
- English: [whisper.cpp JFK sample](https://github.com/ggml-org/whisper.cpp/blob/master/samples/jfk.wav), locally downloaded to ignored `artifacts/samples/jfk.wav`.
- Chinese: `chinese-weather.wav`, generated locally with the installed Microsoft Huihui Desktop voice at 16 kHz mono PCM. It is an ignored test artifact and is not distributed.
- Russian: an 8-second excerpt from [Wikimedia Commons' spoken Russian article](https://commons.wikimedia.org/wiki/File:Ru-Russian_language_(intro).ogg), CC BY 3.0, speakers Andrew Krizhanovsky and Nataly Lugovaya. The locally decoded excerpt is an ignored test artifact and is not distributed.

## Real results

| Path | Observed result | Timing |
| --- | --- | --- |
| English WAV through system loopback → Chinese floating subtitle | The window showed `what your country can do for you.` and `您的国家可以为您做些什么。` | No synchronized phrase-end measurement |
| Chinese WAV through system loopback → English floating subtitle | The window showed `今天天气很好` and `It's a nice day.` | No synchronized phrase-end measurement |
| Russian WAV through system loopback → Chinese floating subtitle | The last displayed fragment was `6 среди всех языков.` and `在所有语言中占6种。`; the fragment translation is awkward | No synchronized phrase-end measurement |
| Chinese WAV → local recognizer → MyMemory → final subtitle update | Detected `zh`; nonempty different English result | 11.43 seconds from rapid PCM submission to final update |
| Russian WAV → local recognizer → MyMemory → final subtitle update | Detected `ru`; nonempty different Chinese result | 11.27 seconds from rapid PCM submission to final update |
| Extracted `win-x64` review package, English WAV through system loopback → Chinese floating subtitle | ZIP hash matched the published `.sha256`; a fresh launch reused the validated model, accepted consent, and showed `ask what you can do for your country.` → `问问你能为你的国家做些什么。`; closing the subtitle window ended the process | No synchronized phrase-end measurement |
| Rebuilt review package after the network-warning fix, English WAV through system loopback → Chinese floating subtitle | A fresh extraction showed `And so, my fellow Americans.` → `还有你，我的美国同胞们。`; a backlog warning also appeared, so this run does not establish continuous real-time throughput | No synchronized phrase-end measurement |

Standalone recognition identified Chinese as `zh` in 9.71 seconds and Russian as `ru` in 9.80 seconds. The Chinese output used traditional character forms. The Russian recognition included `язык русского народа. Является одним из наиболее распространенных языков мира, шестым среди всех языков.`

Six live MyMemory text directions (`en↔zh`, `en↔ru`, `zh↔ru`) returned nonempty changed results without an account or key. Individual requests took 0.5–2.2 seconds in this run. The service was intermittently unavailable on an earlier live chain run; a short successful run does not establish long-term availability.

After the network-warning fix and final package rebuild, the three opt-in live audio chains passed again with the same local fixtures: English → Chinese in 15.36 seconds, Chinese → English in 15.31 seconds, and Russian → Chinese in 19.03 seconds, measured from rapid PCM submission to a final update. These slower values reinforce that the trial has not met a low-latency real-time target.

## Recovery and remaining limits

Automated tests cover a bounded two-item utterance queue, a 500-byte request limit, HTTP and quota classification, 1/2/4-second transient network retries, continued processing after retries are exhausted, and a visible warning that clears after a later successful translation. The real network adapter was not disconnected for this check. An app-scoped proxy recovery test was attempted, but the sandbox denied the loopback socket connection before the app could use it; this is not evidence of application recovery. The extracted app started a microphone session with the selected Realtek device, but a WAV played through speakers produced no recognized sentence. During a separate 9-second playback, the speaker peak meter reached 0.64 while the active, unmuted Realtek microphone meter remained at 0. The microphone translation path therefore remains unverified in this environment.

The CPU recognizer adds roughly 10 seconds per sample in these tests. The values above include model processing and a network request but start when prerecorded PCM is submitted quickly; they are **not** measured phrase-end-to-screen latency. The 8-second utterance cap can split Russian speech and reduce translation quality. MyMemory's anonymous allowance is [5,000 characters per day](https://mymemory.translated.net/doc/usagelimits.php). A 30-minute end-to-end stability claim is not supported.

No Azure credentials were configured or used. The default published desktop folder and extracted review package were inspected and contained no Azure Speech SDK binaries after the optional adapter was separated. The package scan also found no model weights, fixture audio, user settings, or logs. All relative links in the bundled Markdown files resolved. This is an owner review package, not a public release or a 30-minute translation acceptance.
