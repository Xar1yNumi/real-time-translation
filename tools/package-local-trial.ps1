param(
    [string]$OutputName = 'RealTimeTranslation-local-zh-en-win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repository 'artifacts'
$output = Join-Path $artifacts $OutputName
$archive = Join-Path $artifacts ($OutputName + '.zip')
$pythonSource = Join-Path $artifacts 'uv-python/cpython-3.11-windows-x86_64-none'
$packagesSource = Join-Path $artifacts 'localtranslation-venv/Lib/site-packages'
$modelsSource = Join-Path $artifacts 'models'

foreach ($path in @($pythonSource, $packagesSource, $modelsSource)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required local source is missing: $path"
    }
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

dotnet publish (Join-Path $repository 'src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    --output $output -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }

$bundle = Join-Path $output 'local-data'
$pythonDestination = Join-Path $bundle 'python'
$modelsDestination = Join-Path $bundle 'models'
New-Item -ItemType Directory -Path $bundle, $modelsDestination -Force | Out-Null
Copy-Item -LiteralPath $pythonSource -Destination $pythonDestination -Recurse
$packageDestination = Join-Path $pythonDestination 'Lib/site-packages'
New-Item -ItemType Directory -Path $packageDestination -Force | Out-Null
Copy-Item -Path (Join-Path $packagesSource '*') -Destination $packageDestination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repository 'tools/LocalTranslation.Worker/worker.py') `
    -Destination (Join-Path $bundle 'worker.py')
Copy-Item -LiteralPath (Join-Path $modelsSource 'ggml-small-q5_1.bin'), `
    (Join-Path $modelsSource 'silero_vad.onnx') -Destination $modelsDestination
Copy-Item -LiteralPath (Join-Path $modelsSource 'opus-mt-en-zh'), `
    (Join-Path $modelsSource 'opus-mt-zh-en') -Destination $modelsDestination -Recurse
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE'), `
    (Join-Path $repository 'THIRD_PARTY_NOTICES.md'), `
    (Join-Path $repository 'README.md'), `
    (Join-Path $repository 'README.zh-CN.md') -Destination $output
Copy-Item -LiteralPath (Join-Path $repository 'third_party_licenses') `
    -Destination $output -Recurse

if (-not (Test-Path -LiteralPath (Join-Path $output 'onnxruntime.dll') -PathType Leaf)) {
    throw 'The ONNX Runtime native DLL was not included in the publish output.'
}
if (Get-ChildItem -LiteralPath $output -Recurse -File |
    Where-Object { $_.Name -match 'Azure|CognitiveServices' }) {
    throw 'The local trial unexpectedly contains an Azure binary.'
}

Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
Set-Content -LiteralPath ($archive + '.sha256') -Value $hash
Write-Output "Package: $archive"
Write-Output "SHA-256: $hash"
