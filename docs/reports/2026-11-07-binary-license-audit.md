# Windows binary redistribution audit, 2026-11-07

**Decision: keep the full ZIP private.** This is an audit candidate, not a cleared GitHub Release asset. Source publication is unaffected.

## Candidate and verification

- Built `artifacts/RealTimeTranslation-local-zh-en-audit-candidate-win-x64.zip` on Windows x64 from the public source baseline plus the packaging changes in this branch.
- ZIP size: 650,545,465 bytes. SHA-256: `B53A51E9F689E08BE0B6077D582034D1A1DB0B2D9EAC8CFF22D451BD7CDE077B`.
- The package includes CPython, the local speech and translation models, the WPF application, model credits, and third-party notices. It excludes unused Python packages and NVIDIA `cudnn64_9.dll`; it retains `libiomp5md.dll` for CPU inference. No Azure binary was found.
- Locked restore and Release build passed. Direct xUnit runners passed 207 non-live tests, with 19 environment-gated tests skipped. `dotnet test` could not connect to its named pipe in the restricted runner, so each test executable was run directly; the infrastructure suite needed Windows access for temporary file moves and Credential Manager.
- The fresh ZIP was extracted and the `BundledLocalTrialLiveTests.ExtractedPackageTranslatesWithoutInstalledPythonOrOnlineService` test passed on the extracted contents. The prior 10-minute stability result belongs to the older owner trial ZIP, not this candidate.

## License and provenance checks

| Component | Finding | Status |
| --- | --- | --- |
| English→Chinese OPUS-MT | [Pinned upstream model card](https://huggingface.co/Helsinki-NLP/opus-mt-en-zh/tree/408d9bc410a388e1d9aef112a2daba955b945255) says Apache-2.0; the [converted archive](https://huggingface.co/Unbakedcookie/live-translator-models/tree/50473df5681764ebdef107dd78f7d21da66c28a7) calls it CC-BY-4.0. Original archive notice is preserved and upstream attribution plus Apache-2.0 text have been added. The conflicting statement should be confirmed with the converter before a public binary release. | Open |
| Chinese→English OPUS-MT | [Pinned upstream card](https://huggingface.co/Helsinki-NLP/opus-mt-zh-en/tree/cf109095479db38d6df799875e34039d4938aaa6) and converted archive both identify CC-BY-4.0. Model and converter credits are included. | Documented |
| Intel OpenMP | Bundled `libiomp5md.dll` has SHA-256 `982233366B0AFCDA1E0F55A0B134097E35B779613F54DDB69E685E6CD06B755F`, identical to the DLL in Intel's `intel-openmp 2025.3.0` Windows package. Its EULA and third-party notices are included. The EULA defines redistributables by a file list that was not present in that package. An [Intel developer guide](https://cdrdv2-public.intel.com/848693/onemkl_developer-guide-windows_2025.1-766692-848693.pdf) instructs distribution of `libiomp5md.dll` with a custom DLL, but we have not established a version-matched designation or all downstream conditions for this package. | **Blocks public ZIP** |
| CTranslate2 native binary | The Windows wheel is MIT but links/embeds third-party native runtime components. The included Intel OpenMP DLL was identified; exact embedded Intel MKL/oneDNN notices for this wheel have not been mapped. | Open |
| Other bundled runtimes | CPython and Python package metadata retain license files. ONNX Runtime MIT and third-party notices, CTranslate2 MIT, SentencePiece Apache-2.0, and .NET runtime third-party notices have been added to `third_party_licenses/`. The candidate still contains NumPy's OpenBLAS and Microsoft VC++ runtime DLLs; their notices remain in NumPy's `dist-info/licenses` tree. | Documented, final review needed |

The Intel EULA also imposes conditions on downstream licensing and liability. Including the EULA as a file does not itself establish permission to redistribute the DLL. This report records technical findings; it is not legal clearance.

## Release gate

1. Obtain a version-matched Intel redistribution designation and resolve the remaining native notices, or change the packaging so the project does not redistribute those components.
2. Confirm the English→Chinese converted model's license metadata, or perform a directly documented conversion from the pinned upstream model.
3. Rebuild from the final public commit, rerun the extracted-package live test and stability test, and publish only the newly verified ZIP and checksum.
