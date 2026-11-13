Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $env:VCPKG_INSTALLATION_ROOT) { throw 'Windows runner has no vcpkg installation.' }
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $repository 'artifacts/cpu-translation-build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $env:RUNNER_TEMP 'CTranslate2-v4.8.4'
git clone --recursive --depth 1 --branch v4.8.4 https://github.com/OpenNMT/CTranslate2.git $source
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch pinned CTranslate2 source.' }
$sourceCommit = (git -C $source rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or
    $sourceCommit -ne '5de572ef60b67a0276c5989ee73d0072808ff4c3') {
    throw 'CTranslate2 tag does not resolve to the audited source commit.'
}
$sourceCommit | Set-Content (Join-Path $output 'ctranslate2-source-commit.txt')

$vcpkg = Join-Path $env:VCPKG_INSTALLATION_ROOT 'vcpkg.exe'
& $vcpkg install 'openblas[threads]:x64-windows'
if ($LASTEXITCODE -ne 0) { throw 'OpenBLAS install failed.' }
$installed = Join-Path $env:VCPKG_INSTALLATION_ROOT 'installed/x64-windows'
$header = Get-ChildItem (Join-Path $installed 'include') -Recurse -Filter 'cblas.h' -File |
    Select-Object -First 1
$importLibrary = Get-ChildItem (Join-Path $installed 'lib') -Filter '*openblas*.lib' -File |
    Select-Object -First 1
if (-not $header -or -not $importLibrary) {
    throw 'OpenBLAS header or import library was not installed by vcpkg.'
}
$build = Join-Path $source 'build-cpu'
$prefix = Join-Path $source 'install-cpu'
cmake -S $source -B $build -G 'Visual Studio 17 2022' -A x64 `
    "-DCMAKE_TOOLCHAIN_FILE=$(Join-Path $env:VCPKG_INSTALLATION_ROOT 'scripts/buildsystems/vcpkg.cmake')" `
    "-DCMAKE_INSTALL_PREFIX=$prefix" `
    "-DOPENBLAS_INCLUDE_DIR=$($header.Directory.FullName)" `
    "-DOPENBLAS_LIBRARY=$($importLibrary.FullName)" `
    -DWITH_MKL=OFF -DWITH_DNNL=OFF -DWITH_CUDA=OFF -DWITH_CUDNN=OFF `
    -DWITH_OPENBLAS=ON -DWITH_RUY=ON -DOPENMP_RUNTIME=NONE -DBUILD_CLI=OFF
if ($LASTEXITCODE -ne 0) { throw 'CPU CMake configure failed.' }
cmake --build $build --config Release --parallel 4
if ($LASTEXITCODE -ne 0) { throw 'CPU CTranslate2 build failed.' }
cmake --install $build --config Release
if ($LASTEXITCODE -ne 0) { throw 'CPU CTranslate2 install failed.' }

$openblas = @(Get-ChildItem (Join-Path $installed 'bin') -Filter '*openblas*.dll' -File)
if ($openblas.Count -ne 1) { throw "Expected one OpenBLAS DLL; found $($openblas.Count)." }
$package = Join-Path $source 'python/ctranslate2'
Copy-Item -LiteralPath (Join-Path $prefix 'bin/ctranslate2.dll') -Destination $package
Copy-Item -LiteralPath $openblas[0].FullName -Destination $package
Copy-Item -LiteralPath (Join-Path $installed 'share/openblas/copyright') `
    -Destination (Join-Path $output 'OpenBLAS-copyright.txt')
Copy-Item -LiteralPath (Join-Path $source 'third_party/ruy/LICENSE') `
    -Destination (Join-Path $output 'Ruy-LICENSE.txt')
python -m pip install --disable-pip-version-check pybind11 setuptools wheel numpy pyyaml
if ($LASTEXITCODE -ne 0) { throw 'Python build dependencies failed.' }
$env:CTRANSLATE2_ROOT = $prefix
Push-Location (Join-Path $source 'python')
try {
    python setup.py bdist_wheel
    if ($LASTEXITCODE -ne 0) { throw 'CPU Python wheel build failed.' }
    Copy-Item -LiteralPath (Get-ChildItem 'dist/ctranslate2-4.8.4-*.whl' -File | Select-Object -First 1 -ExpandProperty FullName) `
        -Destination $output
} finally { Pop-Location }

$wheel = Get-ChildItem $output -Filter '*.whl' -File | Select-Object -First 1
Get-FileHash -LiteralPath $wheel.FullName -Algorithm SHA256 | `
    Select-Object -ExpandProperty Hash | Set-Content (Join-Path $output 'wheel.sha256')
$testRuntime = Join-Path $env:RUNNER_TEMP 'cpu-wheel-test'
python -m pip install --disable-pip-version-check --no-deps --target $testRuntime $wheel.FullName
if ($LASTEXITCODE -ne 0) { throw 'Wheel installation failed.' }
$env:PYTHONPATH = $testRuntime
python -c 'import ctranslate2; print(ctranslate2.__version__); print(ctranslate2.get_supported_compute_types("cpu"))'
if ($LASTEXITCODE -ne 0) { throw 'CPU wheel import failed.' }
python -c 'import ctypes, pathlib, ctranslate2; dll=pathlib.Path(ctranslate2.__file__).with_name("openblas.dll"); lib=ctypes.CDLL(str(dll)); lib.openblas_get_config.restype=ctypes.c_char_p; config=lib.openblas_get_config().decode(); print(config); assert "SINGLE_THREADED" not in config and lib.openblas_get_parallel() == 1, "OpenBLAS must use the thread-safe pthread backend"'
if ($LASTEXITCODE -ne 0) { throw 'OpenBLAS threaded build verification failed.' }
$forbidden = Get-ChildItem $testRuntime -Recurse -File | Where-Object Name -Match 'iomp|mkl|cudnn|cublas'
if ($forbidden) { throw "Forbidden native runtime found: $($forbidden.FullName -join ', ')" }
