# Azure live-test audio fixtures

Place three project-owned speech recordings in this directory:

- `zh-CN.wav` — spoken Mandarin Chinese
- `en-US.wav` — spoken English
- `ru-RU.wav` — spoken Russian

Each file must be PCM WAV, 16 kHz, 16-bit, mono, and should contain one clear sentence followed by a short silence. Do not use copyrighted third-party recordings.

For every WAV file, add a matching `<filename>.license.txt` file confirming that the contributor owns the recording and releases it for redistribution under the project's MIT license.

## Deferred validation status

The project owner explicitly deferred Azure live validation on 2026-10-29 so local audio implementation could continue. The following evidence is still pending and must not be reported as passed:

- the `Category=AzureLive` test with real Endpoint and Key;
- the three project-owned recordings and matching license files;
- all six cross-language translation directions;
- measured continuous-language-identification and callback latency results.
