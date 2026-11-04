# Captures current game content with private project/player data and preserves
# the normal HDR/renderer pipeline. Raw PNGs and logs stay outside repositories.
param(
    [string]$Python = 'python',
    [string]$Output = '',
    [string[]]$Only = @()
)
$ErrorActionPreference = 'Stop'
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Output) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $Output = [IO.Path]::GetFullPath((Join-Path $projectDirectory "../art_sources/cthangover/site/documentation/capture_$stamp"))
}
$captureArguments = @('-X', 'utf8', (Join-Path $PSScriptRoot 'capture-shots.py'), '--source', $projectDirectory, '--output', $Output)
foreach ($name in $Only) { $captureArguments += @('--only', $name) }
& $Python @captureArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Only.Count -eq 0) {
    & $Python -X utf8 (Join-Path $PSScriptRoot 'shots-to-img.py') (Join-Path $Output 'captures') (Join-Path $PSScriptRoot 'img/shots')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Write-Output "Capture archive: $Output"
exit 0
