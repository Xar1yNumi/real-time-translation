# Third-party notices for the local desktop preview

The source project uses the components below. A local trial ZIP also bundles runtime files and model data. The binary package remains a private audit candidate until its redistribution questions are resolved.

| Component | Version | License and attribution |
| --- | --- | --- |
| [NAudio](https://github.com/naudio/NAudio) | 3.3.0 | MIT; Copyright © Mark Heath 2026 |
| [OpenccNetLib](https://github.com/laisuk/OpenccNet) | 1.7.0 | MIT; Chinese script and regional phrase conversion. Its embedded OpenCC dictionaries are Apache-2.0; see [the bundled dictionary license](third_party_licenses/OpenCC-Apache-2.0.txt). |
| [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) | 0.8.8 | MIT; Copyright © Oleg Stepanischev 2026; OpenccNetLib dictionary decompression. |
| [System.Formats.Cbor](https://github.com/dotnet/runtime) | 8.0.0 | MIT; Copyright © Microsoft Corporation; OpenccNetLib dependency. |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Copyright (c) 2024 sandrohanea |
| [Whisper.net.Runtime / whisper.cpp](https://github.com/ggml-org/whisper.cpp) | 1.9.1 | MIT; Copyright (c) 2023-2026 The ggml authors |
| [Whisper.net.Runtime.Vulkan / whisper.cpp](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Vulkan accelerated runtime of whisper.cpp |
| [Silero VAD](https://github.com/snakers4/silero-vad) | pinned commit 60b7ffa | MIT; Silero Team; bundled ONNX speech activity model. |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | 1.23.2 | MIT; managed .NET package and matching Windows native DLL. See [MIT](third_party_licenses/ONNXRuntime-MIT.txt) and [third-party notices](third_party_licenses/ONNXRuntime-1.23.2-ThirdPartyNotices.txt). |
| [CPython](https://www.python.org/) | 3.11 | Python Software Foundation License; bundled local translation runtime. |
| [CTranslate2](https://github.com/OpenNMT/CTranslate2) | 4.8.4 | MIT; local machine translation inference. See [MIT](third_party_licenses/CTranslate2-MIT.txt). The Windows wheel also includes the Intel OpenMP DLL below. |
| [Intel OpenMP](https://www.intel.com/content/www/us/en/developer/tools/oneapi/overview.html) | 2025.3.0 | `libiomp5md.dll` matches Intel's official wheel by SHA-256. See its [EULA](third_party_licenses/Intel-OpenMP-2025.3.0-EULA.txt) and [third-party notices](third_party_licenses/Intel-OpenMP-2025.3.0-third-party-programs.txt). Exact version-matched redistribution designation is still unresolved. |
| [SentencePiece](https://github.com/google/sentencepiece) | 0.2.1 | Apache-2.0; model tokenization. See [license](third_party_licenses/SentencePiece-Apache-2.0.txt). |
| [OpenCC Python](https://github.com/yichen0831/opencc-python) | 0.1.7 | Apache-2.0; simplified Chinese conversion. |
| [OPUS-MT English→Chinese](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh/tree/408d9bc410a388e1d9aef112a2daba955b945255) | pinned revision `408d9bc` | Apache-2.0 according to the upstream model card at this revision; Helsinki-NLP / OPUS-MT. The third-party converted archive labels it CC-BY-4.0. Both attributions are retained in [model credits](third_party_licenses/OPUS-MT-model-credits.txt); this discrepancy still needs resolution before public redistribution. |
| [OPUS-MT Chinese→English](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en/tree/cf109095479db38d6df799875e34039d4938aaa6) | pinned revision `cf109095` | [CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/); Helsinki-NLP / OPUS-MT. Converted to CTranslate2 by Live Translator. |

The MIT permission notice for the MIT components follows. OpenCC dictionary data is covered separately by the bundled Apache-2.0 license. Each upstream repository contains its own license and further attribution information.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

The local owner trial bundles a [pinned whisper.cpp speech model](https://huggingface.co/ggerganov/whisper.cpp), the Silero VAD model, two OPUS-MT directions, and a CPython environment. Python package metadata and license files remain in that runtime folder, including NumPy's third-party notices. The audit candidate trims unused Python packages and removes the unused NVIDIA cuDNN DLL. It retains Intel OpenMP for CPU translation. The remaining questions and provenance checks are recorded in the [binary license audit](docs/reports/2026-11-07-binary-license-audit.md); these notices alone do not clear the ZIP for public redistribution.

The optional Azure adapter and Microsoft Cognitive Services Speech SDK are retained as source project dependencies for later evaluation. They are not included in the default desktop package.
