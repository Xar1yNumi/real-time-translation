# Public Windows ZIP without Intel runtime

## Decision

Keep the existing C# WPF desktop application, Python translation worker, and two Chinese–English OPUS-MT directions. Replace only the bundled CTranslate2 Windows wheel and third-party converted model files. Build a pinned CTranslate2 4.8.4 CPU wheel from source with `WITH_MKL=OFF`, `WITH_DNNL=OFF`, `WITH_CUDA=OFF`, `WITH_CUDNN=OFF`, `WITH_OPENBLAS=ON`, `WITH_RUY=ON`, and `OPENMP_RUNTIME=NONE`. The build must contain no Intel OpenMP or MKL binaries. OpenBLAS and Ruy notices must be included. GitHub Actions' Windows runner supplies the C++ compiler because the owner PC has no installed MSVC toolchain.

Convert the two pinned Helsinki-NLP model revisions directly with CTranslate2's OPUS-MT or Transformers converter. Retain upstream license and author credits, tokenizer files, conversion command, original download hashes, and resulting model hashes. Do not use the converted Live Translator archives in the public ZIP.

## Delivery and boundaries

The release remains a self-contained Windows x64 ZIP. First launch may verify files but must not require Azure, another account, Python installation, or network access. The desktop interface and audio pipeline remain unchanged. The package script must reject Intel/NVIDIA DLLs and the old converted model hashes. It must include notices for every native dependency that is redistributed.

## Acceptance

On the owner PC, direct text probes for both directions must produce readable output; the existing extracted-ZIP live test must pass; warm translation P95 for the eight fixed sentences must be at most 250 ms per direction; and the 10-minute low-duty-cycle run must pass without queue/order faults. Verify package SHA-256, archive extraction, executable launch, and absence of Intel/NVIDIA binaries. If any gate fails, keep the old private candidate and do not publish a Release asset.

The public GitHub Release contains the verified ZIP, matching checksum, operating instructions, limitations, and license notices. The source branch is merged only after CI and local checks pass.
