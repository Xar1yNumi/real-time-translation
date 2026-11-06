# Third-party notices for the default desktop package

The default Windows trial includes these components:

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
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | 1.23.2 | MIT; managed .NET package and matching Windows native DLL. |
| [CPython](https://www.python.org/) | 3.11 | Python Software Foundation License; bundled local translation runtime. |
| [CTranslate2](https://github.com/OpenNMT/CTranslate2) | 4.8.4 | MIT; local machine translation inference. |
| [SentencePiece](https://github.com/google/sentencepiece) | 0.2.1 | Apache-2.0; model tokenization. |
| [OpenCC Python](https://github.com/yichen0831/opencc-python) | 0.1.7 | Apache-2.0; simplified Chinese conversion. |
| [OPUS-MT English↔Chinese](https://huggingface.co/Helsinki-NLP) | pinned revisions in model metadata | CC-BY-4.0; Helsinki-NLP / OPUS-MT. The bundled model folders retain their LICENCE.txt files and conversion metadata. |

The MIT permission notice for the MIT components follows. OpenCC dictionary data is covered separately by the bundled Apache-2.0 license. Each upstream repository contains its own license and further attribution information.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

The local owner trial bundles a [pinned whisper.cpp speech model](https://huggingface.co/ggerganov/whisper.cpp), the Silero VAD model, two OPUS-MT directions, and a Python environment. Python's installed package metadata and license files remain in the runtime folder. A public binary release requires a separate review of every transitive package's notices and model distribution terms.

The optional Azure adapter and Microsoft Cognitive Services Speech SDK are retained as source project dependencies for later evaluation. They are not included in the default desktop package.
