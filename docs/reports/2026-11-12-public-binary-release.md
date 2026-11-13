# Windows x64 public binary release verification, 2026-11-12

## Build inputs

- Source branch: `codex/no-intel-public-release`, based on public `main` at `8638638`.
- CTranslate2 4.8.4 source commit `5de572ef60b67a0276c5989ee73d0072808ff4c3`, compiled by the Windows [CPU workflow](../../.github/workflows/build-cpu-translation.yml) with OpenBLAS and Ruy; MKL, oneDNN, CUDA, cuDNN and OpenMP disabled.
- Audited CPU wheel SHA-256: `A8375961B786F6D7971EB78582197AFC9698326A004F617BFCF4E0916C683A8E`.
- The final ZIP rebuild reuses this audited wheel. The build script now checks the source commit and OpenBLAS DLL hash, but the hosted vcpkg baseline is not fixed; a fresh compilation may differ and would require a separate audit and pin update before release use.
- OPUS-MT models were independently converted from the two pinned Helsinki-NLP revisions and hash-checked against [the source manifest](../../tools/LocalTranslation.Probe/upstream-model-manifest.json). The previous third-party converted archives are not package inputs.
- The public ZIP includes CPython 3.11, the pinned Whisper and Silero models, the CPU wheel, OPUS-MT models, the self-contained .NET desktop application and applicable license notices. Conversion-only PyTorch and Transformers packages are excluded.

## Candidate checks

- The `RealTimeTranslation-public-cpu-candidate-r6-win-x64.zip` candidate is 625,282,295 bytes, SHA-256 `425F0315C1C3D05A894ECB715B8692F45DD102B0074019B83933781E409D0CDB`. The ZIP was freshly extracted with no manual file additions.
- Native package scan found `ctranslate2.dll` SHA-256 `2147B3E58E5C2380601025D588DE76B7931F8B17100D976CD69F9104129CAE15` and `openblas.dll` SHA-256 `75E0F2E92A8DED274CF519473989C5FBF891DD9F8326C57673A61E27FAD557A9`. No file name matching Intel MKL/OpenMP or NVIDIA CUDA/cuDNN appeared.
- On 2026-11-13, the final Release build completed with zero warnings or errors; four direct .NET test runners passed 207 tests, skipped 19 environment-gated tests and failed none. The infrastructure runner required normal Windows user permissions for settings and Credential Manager tests. `dotnet test` under this local runner previously returned code 5 with zero discovered tests, so direct xUnit executables were used. The CI `dotnet format --verify-no-changes --no-restore` check also passed.
- The freshly extracted `r6` candidate passed three CPU worker tests: four independent worker startups in both directions, 64 consecutive requests, and a deterministic simulated native stdout warning that did not enter the JSON pipe. It also passed the full English and Chinese WAV → speech recognition → subtitle translation test in 33.393 seconds. The official JFK WAV matched its pinned SHA-256; the Chinese sample was generated with the installed Microsoft Huihui Desktop voice.
- On the extracted `r6` package, the fixed eight-sentence probe measured warm translation P95 of **216.88 ms English→Chinese** and **249.41 ms Chinese→English**. Four additional runs measured 216.35–219.52 ms and 235.85–249.01 ms respectively. All five runs met the 250 ms per-direction design gate on this owner PC, with little margin on Chinese→English. These figures exclude speech recognition, model startup and audio segmentation; they are not a general screen-latency guarantee.
- A diagnostic copy using the same `r6` worker source passed six consecutive two-direction WAV sessions. The freshly extracted `r6` package then passed its own 10-minute low-duty-cycle continuity test in **625.831 seconds**, checking ordered subtitles, bounded queues, no translation cancellations, and bounded process memory growth. Its desktop executable was still running 12 seconds after launch and was then stopped; this checks startup, not manual UI experience.

## Rejected candidates and remaining native risk

The older `r4` ZIP passed an isolated 10-minute low-duty-cycle run in 625.878 seconds, but later repeated live tests exposed intermittent OpenBLAS allocator warnings inside the JSON pipe and one corrupted translation with its three-thread setting. `r4` and `r5` must not be published. The `r6` worker duplicates the protocol pipe and redirects native stdout before loading CTranslate2, and reduces intra-operation threads to two. No allocator warning entered the protocol in the new worker tests or six diagnostic WAV runs. The native OpenBLAS build reports `Bad memory unallocation` in benchmark logs even with two threads; its cause is unresolved. **Do not upload `r6` as a public binary.** A thread-safe OpenBLAS rebuild is being investigated.

## Scope and limitations

The public package supports Chinese↔English only, capturing the default system playback stream or a microphone. It does not isolate individual applications or speakers. Music and background noise can reduce recognition accuracy. OPUS-MT output varies with CPU parallelism and can omit part of a sentence; tests check direction and readable meaning, not exact wording. The owner waived live microphone testing. Physical network disconnection and a 30-minute high-duty-cycle run have not been measured; translation itself uses local components.

The source repository includes historical notes about an older, private Intel-based audit candidate. That candidate is not the public release binary.
