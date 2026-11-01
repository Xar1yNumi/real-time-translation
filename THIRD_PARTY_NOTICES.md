# Third-party notices for the default desktop package

The default Windows trial includes these components:

| Component | Version | License and attribution |
| --- | --- | --- |
| [NAudio](https://github.com/naudio/NAudio) | 3.3.0 | MIT; Copyright © Mark Heath 2026 |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Copyright (c) 2024 sandrohanea |
| [Whisper.net.Runtime / whisper.cpp](https://github.com/ggml-org/whisper.cpp) | 1.9.1 | MIT; Copyright (c) 2023-2026 The ggml authors |
| [Whisper.net.Runtime.Vulkan / whisper.cpp](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT; Vulkan accelerated runtime of whisper.cpp |

The MIT permission notice for these components follows. Each upstream repository contains its own license and further attribution information.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

The speech model is downloaded separately from a [pinned whisper.cpp model source](https://huggingface.co/ggerganov/whisper.cpp). It is not bundled in the ZIP. MyMemory is a remote service, not code distributed in this package; its [terms](https://mymemory.translated.net/terms-and-conditions) apply to submitted text.

The optional Azure adapter and Microsoft Cognitive Services Speech SDK are retained as source project dependencies for later evaluation. They are not included in the default desktop package.
