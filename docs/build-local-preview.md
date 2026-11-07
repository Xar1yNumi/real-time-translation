# Build the local Chinese–English preview

This guide is for Windows x64 contributors who want to assemble a complete local trial from the public source. It downloads approximately 1 GB of model data plus a Python runtime and packages. The official GitHub repository does not currently distribute a ready-to-run binary.

## Prerequisites

- .NET SDK 10.0.301 (see [global.json](../global.json)).
- [uv](https://docs.astral.sh/uv/getting-started/installation/) available on PATH.
- Enough disk space for model archives, extraction, the runtime, and the final ZIP.

Run these commands in the repository root in PowerShell. Everything below stays under Git-ignored artifacts/.

```powershell
New-Item -ItemType Directory -Force artifacts/models | Out-Null
$env:UV_PYTHON_INSTALL_DIR = (Join-Path $PWD 'artifacts/uv-python')
$env:UV_CACHE_DIR = (Join-Path $PWD 'artifacts/uv-cache')
uv python install 3.11.17
uv venv --python 3.11.17 artifacts/localtranslation-venv
uv pip install --python artifacts/localtranslation-venv/Scripts/python.exe -r tools/LocalTranslation.Probe/requirements.txt

curl.exe -L --fail -o artifacts/models/ggml-small-q5_1.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin
curl.exe -L --fail -o artifacts/models/silero_vad.onnx https://raw.githubusercontent.com/snakers4/silero-vad/60b7ffa243625ebdc1070275a29f18c87843786a/src/silero_vad/data/silero_vad.onnx
curl.exe -L --fail -o artifacts/models/opus-mt-en-zh.zip https://huggingface.co/Unbakedcookie/live-translator-models/resolve/50473df5681764ebdef107dd78f7d21da66c28a7/opus-mt-en-zh.zip
curl.exe -L --fail -o artifacts/models/opus-mt-zh-en.zip https://huggingface.co/Unbakedcookie/live-translator-models/resolve/50473df5681764ebdef107dd78f7d21da66c28a7/opus-mt-zh-en.zip
```

Verify the four downloads before extraction. These are the hashes used in the owner trial; stop if any value differs.

| File | SHA-256 |
| --- | --- |
| ggml-small-q5_1.bin | AE85E4A935D7A567BD102FE55AFC16BB595BDB618E11B2FC7591BC08120411BB |
| silero_vad.onnx | 1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3 |
| opus-mt-en-zh.zip | 1748DFA40AADF2AEA67EFF4D6845D8AABD3093604FC22125B7232F17F404947 |
| opus-mt-zh-en.zip | 5AA44CB302EA85132B041B5C5DD7E76DBAD92AA4CC4537674EE4389A0DBD3CEF |

```powershell
Get-FileHash artifacts/models/ggml-small-q5_1.bin, artifacts/models/silero_vad.onnx, artifacts/models/opus-mt-en-zh.zip, artifacts/models/opus-mt-zh-en.zip -Algorithm SHA256
Expand-Archive artifacts/models/opus-mt-en-zh.zip artifacts/models/opus-mt-en-zh
Expand-Archive artifacts/models/opus-mt-zh-en.zip artifacts/models/opus-mt-zh-en
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
powershell -File tools/package-local-trial.ps1
```

The packaging script verifies the model weights again and writes a ZIP plus .sha256 under artifacts/. A normal source build alone does not produce a working translation app: the desktop UI requires local-data/ with the Python worker and models. Keep the entire extracted package together.

The OPUS-MT archives are third-party CTranslate2 conversions. The English-to-Chinese archive's license metadata conflicts with its [pinned upstream model card](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh/tree/408d9bc410a388e1d9aef112a2daba955b945255). This guide enables personal builds; it does not clear the ZIP for redistribution. See the [release checklist](release-checklist.md).
