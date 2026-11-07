Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest = Get-Content (Join-Path $PSScriptRoot 'LocalTranslation.Probe/upstream-model-manifest.json') -Raw |
    ConvertFrom-Json
$converter = Join-Path $repository 'artifacts/localtranslation-venv/Scripts/ct2-transformers-converter.exe'
if (-not (Test-Path -LiteralPath $converter -PathType Leaf)) {
    throw 'Install the pinned conversion dependencies before preparing upstream models.'
}

foreach ($direction in @('en-zh', 'zh-en')) {
    $entry = $manifest.PSObject.Properties[$direction].Value
    $source = Join-Path $repository "artifacts/upstream-models/opus-mt-$direction"
    $destination = Join-Path $repository "artifacts/models-public/opus-mt-$direction"
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    foreach ($file in $entry.files.PSObject.Properties) {
        $path = Join-Path $source $file.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Value) {
            $url = "https://huggingface.co/$($entry.repo)/resolve/$($entry.revision)/$($file.Name)"
            curl.exe --location --fail --retry 3 --silent --show-error --output $path $url
            if ($LASTEXITCODE -ne 0) { throw "Model download failed: $url" }
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Value) {
                throw "Upstream model hash mismatch: $($entry.repo)/$($file.Name)"
            }
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $destination 'model.bin') -PathType Leaf) -or
        (Get-FileHash -LiteralPath (Join-Path $destination 'model.bin') -Algorithm SHA256).Hash -ne
            $entry.converted_model_sha256) {
        & $converter --model $source --output_dir $destination --quantization float16 `
            --copy_files source.spm target.spm --force
        if ($LASTEXITCODE -ne 0) { throw "Model conversion failed: $direction" }
    }
    if ((Get-FileHash -LiteralPath (Join-Path $destination 'model.bin') -Algorithm SHA256).Hash -ne
        $entry.converted_model_sha256) {
        throw "Converted model hash mismatch: $direction"
    }
    $metadata = [ordered]@{
        repo = $entry.repo
        revision = $entry.revision
        licence = $entry.license
        language_tag = $entry.language_tag
        conversion = 'CTranslate2 4.8.4 ct2-transformers-converter --quantization float16'
    }
    $metadata | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $destination 'opus_mt.json') -Encoding utf8
    Write-Output "Verified upstream model and conversion: $direction"
}
