param(
    [string]$OutputName = 'RealTimeTranslation-local-zh-en-win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($OutputName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $OutputName.Contains('..')) {
    throw 'OutputName must be a simple file name under artifacts.'
}

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repository 'artifacts'
$output = Join-Path $artifacts $OutputName
$archive = Join-Path $artifacts ($OutputName + '.zip')
$pythonSource = Join-Path $artifacts 'uv-python/cpython-3.11-windows-x86_64-none'
$packagesSource = Join-Path $artifacts 'localtranslation-venv/Lib/site-packages'
$modelsSource = Join-Path $artifacts 'models-public'

foreach ($path in @($pythonSource, $packagesSource, $modelsSource)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required local source is missing: $path"
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $repository 'tools/LocalTranslation.Probe/upstream-model-manifest.json') -Raw |
    ConvertFrom-Json
$wheel = Join-Path $artifacts 'cpu-translation-threaded-build/ctranslate2-4.8.4-cp311-cp311-win_amd64.whl'
if (-not (Test-Path -LiteralPath $wheel -PathType Leaf) -or
    (Get-FileHash -LiteralPath $wheel -Algorithm SHA256).Hash -ne
        '8DF9A7B07A9E1B380D1048F26736F6565C98078CEF1548C82EC4B58089C26EFC') {
    throw 'Independent CPU translation wheel is missing or differs from the audited CI artifact.'
}
if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $archive)) {
    throw "The output already exists. Choose a new -OutputName: $OutputName"
}

$expected = @{
    'ggml-small-q5_1.bin' = 'AE85E4A935D7A567BD102FE55AFC16BB595BDB618E11B2FC7591BC08120411BB'
    'silero_vad.onnx' = '1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3'
    'opus-mt-en-zh/model.bin' = 'F24C2BB82368F7DE0196882DE1D8B644D2AA54AE2439C3142F263DE8A64EA2A9'
    'opus-mt-zh-en/model.bin' = 'A188BC45BCE24635A1EB0CC42AD0B43AFDAD806B86AF9BEB3065BBA11F3B212C'
}
foreach ($entry in $expected.GetEnumerator()) {
    $source = Join-Path $modelsSource $entry.Key
    if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Value) {
        throw "Model source is missing or modified: $($entry.Key)"
    }
}
foreach ($direction in @('en-zh', 'zh-en')) {
    $entry = $manifest.PSObject.Properties[$direction].Value
    $modelDir = Join-Path $modelsSource "opus-mt-$direction"
    $metadataPath = Join-Path $modelsSource "opus-mt-$direction/opus_mt.json"
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ($metadata.repo -ne $entry.repo -or
        $metadata.revision -ne $entry.revision -or
        $metadata.licence -ne $entry.license -or
        $metadata.language_tag -ne $entry.language_tag -or
        $metadata.conversion -ne 'CTranslate2 4.8.4 ct2-transformers-converter --quantization float16') {
        throw "Model provenance is not the audited upstream conversion: $direction"
    }
    $allowed = @($entry.converted_files.PSObject.Properties.Name) + @('opus_mt.json')
    $actual = @(Get-ChildItem -LiteralPath $modelDir -Recurse -File | ForEach-Object {
        $_.FullName.Substring($modelDir.Length + 1).Replace('\', '/')
    })
    if (@($actual | Where-Object { $_ -notin $allowed }).Count -gt 0 -or
        @($allowed | Where-Object { $_ -notin $actual }).Count -gt 0) {
        throw "Model directory has missing or extra files: $direction"
    }
    foreach ($file in $entry.converted_files.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $modelDir $file.Name) -Algorithm SHA256).Hash -ne
            $file.Value) {
            throw "Converted model file differs from the audited manifest: $direction/$($file.Name)"
        }
    }
}

dotnet publish (Join-Path $repository 'src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    --output $output -p:RestoreLockedMode=true -m:1 -nr:false
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }

$bundle = Join-Path $output 'local-data'
$pythonDestination = Join-Path $bundle 'python'
$modelsDestination = Join-Path $bundle 'models'
New-Item -ItemType Directory -Path $bundle, $modelsDestination -Force | Out-Null
Copy-Item -LiteralPath $pythonSource -Destination $pythonDestination -Recurse
$packageDestination = Join-Path $pythonDestination 'Lib/site-packages'
if ((Test-Path -LiteralPath $packageDestination) -and
    [IO.Path]::GetFullPath($packageDestination).StartsWith(
        [IO.Path]::GetFullPath($output) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $packageDestination -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDestination -Force | Out-Null
$runtimePackages = @(
    'sentencepiece', 'sentencepiece-0.2.1.dist-info',
    'opencc', 'opencc_python_reimplemented-0.1.7.dist-info',
    'yaml', '_yaml', 'pyyaml-6.0.3.dist-info'
)
foreach ($package in $runtimePackages) {
    $source = Join-Path $packagesSource $package
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required translation runtime package is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination $packageDestination -Recurse
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($wheel, $packageDestination)
$cpuDll = Join-Path $packageDestination 'ctranslate2/ctranslate2.dll'
$blasDll = Join-Path $packageDestination 'ctranslate2/openblas.dll'
if ((Get-FileHash -LiteralPath $cpuDll -Algorithm SHA256).Hash -ne
        '1126D423BD475192FDF0640E0B7A13B697CA4829D51AA4995E1DF46FCA63C169' -or
    (Get-FileHash -LiteralPath $blasDll -Algorithm SHA256).Hash -ne
        'EB36FC978B0C07A4183061ED787FA381B1A7A89A2C908544FE690662B80B4F9A') {
    throw 'Packaged CPU native DLLs differ from the audited wheel.'
}
Copy-Item -LiteralPath (Join-Path $repository 'tools/LocalTranslation.Worker/worker.py') `
    -Destination (Join-Path $bundle 'worker.py')
Copy-Item -LiteralPath (Join-Path $modelsSource 'ggml-small-q5_1.bin'), `
    (Join-Path $modelsSource 'silero_vad.onnx') -Destination $modelsDestination
Copy-Item -LiteralPath (Join-Path $modelsSource 'opus-mt-en-zh'), `
    (Join-Path $modelsSource 'opus-mt-zh-en') -Destination $modelsDestination -Recurse
foreach ($direction in @('en-zh', 'zh-en')) {
    $metadata = Get-Content -LiteralPath (Join-Path $modelsSource "opus-mt-$direction/opus_mt.json") -Raw |
        ConvertFrom-Json
    @(
        "$($metadata.repo) at revision $($metadata.revision)"
        "License: $($metadata.licence)"
        'Converted independently from the pinned upstream model using CTranslate2 4.8.4 (float16).'
        'See ../MODEL-CREDITS.txt and ../../../third_party_licenses/ for full attribution and license texts.'
    ) | Set-Content -LiteralPath (Join-Path $modelsDestination "opus-mt-$direction/LICENCE.txt") -Encoding utf8
}
Copy-Item -LiteralPath (Join-Path $repository 'third_party_licenses/OPUS-MT-model-credits.txt') `
    -Destination (Join-Path $modelsDestination 'MODEL-CREDITS.txt')
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE'), `
    (Join-Path $repository 'THIRD_PARTY_NOTICES.md'), `
    (Join-Path $repository 'START-HERE.txt'), `
    (Join-Path $repository 'README.md'), `
    (Join-Path $repository 'README.zh-CN.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $repository 'third_party_licenses') `
    -Destination $output -Recurse
$noticeDir = Join-Path $output 'third_party_licenses'
Get-ChildItem -LiteralPath $noticeDir -Filter 'Intel-OpenMP-*' -File |
    Remove-Item

$onnxDll = Join-Path $output 'onnxruntime.dll'
if (-not (Test-Path -LiteralPath $onnxDll -PathType Leaf) -or
    (Get-FileHash -LiteralPath $onnxDll -Algorithm SHA256).Hash -ne
        '93457C4F17904D0AB98F3E9AC27ED153993F4B5B769337CBFD6F4C528FB0A509') {
    throw 'ONNX Runtime native DLL is missing or differs from the audited 1.23.2 build.'
}
if (Get-ChildItem -LiteralPath $output -Recurse -File |
    Where-Object { $_.Name -match 'Azure|CognitiveServices' }) {
    throw 'The local trial unexpectedly contains an Azure binary.'
}
if (Get-ChildItem -LiteralPath $output -Recurse -File -Include *.dll, *.pyd |
    Where-Object { $_.Name -match 'iomp|mkl|cudnn|cublas|cudart' }) {
    throw 'The local trial unexpectedly contains an Intel or NVIDIA native runtime.'
}
if (Get-ChildItem -LiteralPath $output -Recurse -File |
    Where-Object { $_.Name -match '^(msvcp140(?:_1|-.*)?|vcomp140)\.dll$' }) {
    throw 'The local trial unexpectedly redistributes Microsoft VC++ runtime DLLs.'
}

Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
Set-Content -LiteralPath ($archive + '.sha256') -Value $hash
Write-Output "Package: $archive"
Write-Output "SHA-256: $hash"
