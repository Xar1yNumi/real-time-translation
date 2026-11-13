# Third-party notices for the local desktop preview

The source project uses the components below. The Windows ZIP bundles the application, Python runtime, and model data. The CPU inference wheel is built from pinned CTranslate2 source with OpenBLAS and Ruy, without Intel MKL, Intel OpenMP, or NVIDIA runtimes. Microsoft Visual C++ Redistributable x64 is an external installation prerequisite: the ZIP does not include `MSVCP140.dll`, `MSVCP140_1.dll`, or `VCOMP140.dll`. Python's standard `vcruntime140` files remain bundled. Install the x64 Redistributable from [Microsoft](https://aka.ms/vc14/vc_redist.x64.exe) if needed.

| Component | Version | License and attribution |
| --- | --- | --- |
| [NAudio](https://github.com/naudio/NAudio) | 3.3.0 | MIT; Copyright © Mark Heath 2026 |
| [OpenccNetLib](https://github.com/laisuk/OpenccNet) | 1.7.0 | MIT; Chinese script and regional phrase conversion. Its embedded OpenCC dictionaries are Apache-2.0; see [the bundled dictionary license](third_party_licenses/OpenCC-Apache-2.0.txt). |
| [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) | 0.8.8 | MIT; Copyright © Oleg Stepanischev 2026; OpenccNetLib dictionary decompression. |
| [System.Formats.Cbor](https://github.com/dotnet/runtime) | 8.0.2 | MIT; Copyright © Microsoft Corporation; OpenccNetLib dependency. |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Copyright (c) 2024 sandrohanea |
| [Whisper.net.Runtime / whisper.cpp](https://github.com/ggml-org/whisper.cpp) | 1.9.1 | MIT; Copyright (c) 2023-2026 The ggml authors |
| [Whisper.net.Runtime.Vulkan / whisper.cpp](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Vulkan accelerated runtime of whisper.cpp |
| [Silero VAD](https://github.com/snakers4/silero-vad) | pinned commit 60b7ffa | MIT; Silero Team; bundled ONNX speech activity model. |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | 1.23.2 | MIT; managed .NET package and matching Windows native DLL. See [MIT](third_party_licenses/ONNXRuntime-MIT.txt) and [third-party notices](third_party_licenses/ONNXRuntime-1.23.2-ThirdPartyNotices.txt). |
| [CPython](https://www.python.org/) | 3.11 | Python Software Foundation License; bundled local translation runtime. |
| [CTranslate2](https://github.com/OpenNMT/CTranslate2) | 4.8.4, source commit `5de572ef` | MIT; local machine translation inference. The CPU wheel is built by this project. See [MIT](third_party_licenses/CTranslate2-MIT.txt). |
| [OpenBLAS](https://github.com/OpenMathLib/OpenBLAS) | vcpkg build in pinned CPU wheel | BSD-3-Clause; CPU matrix operations. See [copyright and terms](third_party_licenses/OpenBLAS-copyright.txt). |
| [Ruy](https://github.com/google/ruy) | pinned CTranslate2 submodule | Apache-2.0; statically linked CPU matrix operations. See [license](third_party_licenses/Ruy-Apache-2.0.txt). |
| [cpu_features](https://github.com/google/cpu_features) | pinned CTranslate2 submodule | Apache-2.0 and BSD-3-Clause; statically linked CPU feature detection. See [license](third_party_licenses/cpu_features-Apache-2.0.txt). |
| [spdlog](https://github.com/gabime/spdlog) and bundled [{fmt}](https://github.com/fmtlib/fmt) | pinned CTranslate2 submodule | MIT; header-only logging and formatting. See [spdlog](third_party_licenses/spdlog-MIT.txt) and [{fmt}](third_party_licenses/fmt-MIT.txt) licenses. |
| [SentencePiece](https://github.com/google/sentencepiece) | 0.2.1 | Apache-2.0; model tokenization. See [license](third_party_licenses/SentencePiece-Apache-2.0.txt). |
| [OpenCC Python](https://github.com/yichen0831/opencc-python) | 0.1.7 | Apache-2.0; simplified Chinese conversion. |
| [OPUS-MT English→Chinese](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh/tree/408d9bc410a388e1d9aef112a2daba955b945255) | pinned revision `408d9bc` | Apache-2.0 according to the upstream model card; Helsinki-NLP / OPUS-MT. Independently converted from upstream files; see [model credits](third_party_licenses/OPUS-MT-model-credits.txt). |
| [OPUS-MT Chinese→English](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en/tree/cf109095479db38d6df799875e34039d4938aaa6) | pinned revision `cf109095` | [CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/); Helsinki-NLP / OPUS-MT. Independently converted from upstream files. |

The MIT permission notice for the MIT components follows. OpenCC dictionary data is covered separately by the bundled Apache-2.0 license. Each upstream repository contains its own license and further attribution information.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

The complete package bundles a [pinned whisper.cpp speech model](https://huggingface.co/ggerganov/whisper.cpp), the Silero VAD model, two independently converted OPUS-MT directions, and CPython. Python package metadata and license files remain in the runtime folder. Conversion-only NumPy is excluded from the release ZIP. The binary build and verification evidence are recorded in the [release report](https://github.com/Xar1yNumi/real-time-translation/blob/main/docs/reports/2026-11-12-public-binary-release.md).

The optional Azure adapter and Microsoft Cognitive Services Speech SDK are retained as source project dependencies for later evaluation. They are not included in the default desktop package.
