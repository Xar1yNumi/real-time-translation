# Build the local Chinese–English preview

The Windows x64 release includes the application, speech and translation models, and a Python runtime. Contributors can reproduce the package from pinned sources. The model and runtime inputs stay under Git-ignored `artifacts/` and are never committed.

## Requirements

- Windows x64, [.NET SDK 10.0.301](../global.json), PowerShell, and [uv](https://docs.astral.sh/uv/getting-started/installation/).
- About 4 GB for upstream models and conversion tools, plus free space for the assembled ZIP.
- The audited CPU wheel artifact from the [CTranslate2 build workflow](../.github/workflows/build-cpu-translation.yml). It compiles CTranslate2 4.8.4 with threaded OpenBLAS and Ruy, and without Intel or NVIDIA inference runtimes. The current release packaging script pins the audited wheel SHA-256 `8DF9A7B07A9E1B380D1048F26736F6565C98078CEF1548C82EC4B58089C26EFC`.

## Prepare runtime and models

Run in the repository root. The Python wheel listed in `requirements.txt` is used as a conversion tool; the release ZIP replaces it with the independently built CPU wheel.

```powershell
$env:UV_PYTHON_INSTALL_DIR = (Join-Path $PWD 'artifacts/uv-python')
$env:UV_CACHE_DIR = (Join-Path $PWD 'artifacts/uv-cache')
uv python install 3.11.17
uv venv --python 3.11.17 artifacts/localtranslation-venv
uv pip install --python artifacts/localtranslation-venv/Scripts/python.exe -r tools/LocalTranslation.Probe/requirements.txt
uv pip install --python artifacts/localtranslation-venv/Scripts/python.exe torch==2.9.1 transformers==4.57.6

New-Item -ItemType Directory -Force artifacts/models-public | Out-Null
curl.exe -L --fail -o artifacts/models-public/ggml-small-q5_1.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin
curl.exe -L --fail -o artifacts/models-public/silero_vad.onnx https://raw.githubusercontent.com/snakers4/silero-vad/60b7ffa243625ebdc1070275a29f18c87843786a/src/silero_vad/data/silero_vad.onnx
powershell -NoProfile -ExecutionPolicy Bypass -File tools/prepare-upstream-models.ps1
```

Verify the speech model SHA-256 values before packaging:

| File | SHA-256 |
| --- | --- |
| `ggml-small-q5_1.bin` | `AE85E4A935D7A567BD102FE55AFC16BB595BDB618E11B2FC7591BC08120411BB` |
| `silero_vad.onnx` | `1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3` |

`prepare-upstream-models.ps1` downloads the exact Helsinki-NLP revisions and verifies the source SHA-256 hashes in [the manifest](../tools/LocalTranslation.Probe/upstream-model-manifest.json). It then converts each model with CTranslate2 and verifies the resulting weights. The release package adds direction-specific attribution files from the upstream metadata.

Extract the CPU workflow artifact into `artifacts/cpu-translation-threaded-build/`, then unpack its wheel into `artifacts/cpu-translation-threaded-wheel/`. For example:

```powershell
Add-Type -AssemblyName System.IO.Compression
[IO.Compression.ZipFile]::ExtractToDirectory(
    (Resolve-Path artifacts/cpu-translation-threaded-build/ctranslate2-4.8.4-cp311-cp311-win_amd64.whl).Path,
    (Join-Path $PWD 'artifacts/cpu-translation-threaded-wheel'))
```

The native ONNX Runtime DLL comes from the pinned `onnxruntime==1.23.2` Python package in the conversion environment. The package script verifies that DLL and the CPU inference wheel against the audited release hashes.

## Build and test

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File tools/package-local-trial.ps1
```

The script writes a complete ZIP and a `.sha256` file under `artifacts/`. It excludes conversion tools and the original upstream weights. Extract the entire ZIP before launching `RealTimeTranslation.Desktop.exe`; moving the EXE alone breaks its local runtime lookup. See the [release checklist](release-checklist.md) and [model credits](../third_party_licenses/OPUS-MT-model-credits.txt) for verification and attribution.
