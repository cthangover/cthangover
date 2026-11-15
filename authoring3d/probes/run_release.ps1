# watch-description: Собрать свежий Windows ExportRelease штатным release-скриптом и выполнить из него opt-in 3D-пробы; NuGet и SDK скрыты, приватный тестовый мод добавляется только на время проверки и затем удаляется. Ожидается матрица возможностей экспортного шаблона с полными логами.
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $out | Out-Null
$env:GODOT_BIN = (Get-Command godot -ErrorAction Stop).Source.Replace('\','/')
$env:PYTHON_BIN = (Get-Command python -ErrorAction Stop).Source.Replace('\','/')
$bash = 'C:/Program Files/Git/bin/bash.exe'
if (Test-Path $bash) {
    $buildMethod = 'build_release.sh windows --no-archive (host Bash)'
    & $bash ./build_release.sh windows --no-archive *> (Join-Path $out 'export.log')
} else {
    $buildMethod = 'build_release.bat windows --no-archive (official Windows twin; host Bash unavailable)'
    & cmd.exe /d /c 'build_release.bat windows --no-archive' *> (Join-Path $out 'export.log')
}
$buildExit = $LASTEXITCODE
@{method=$buildMethod; exit_code=$buildExit; executable=$env:GODOT_BIN} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $out 'build.json')
if ($buildExit -ne 0) { Get-Content (Join-Path $out 'export.log') -Tail 60; exit $buildExit }
$release = Join-Path $root 'releases/windows'
$privateMod = Join-Path $release 'mods_private/world3d_base'
if (Test-Path $privateMod) { throw 'Unexpected existing private probe mod in fresh export' }
New-Item -ItemType Directory -Force $privateMod | Out-Null
try {
    Copy-Item 'mods_private/world3d_base/manifest.json' $privateMod
    Copy-Item -Recurse 'mods_private/world3d_base/src' $privateMod
    New-Item -ItemType Directory -Force (Join-Path $privateMod 'test_suites') | Out-Null
    Copy-Item -Recurse 'mods_private/world3d_base/test_suites/runtime_probe' (Join-Path $privateMod 'test_suites')
    Get-ChildItem $release -Recurse -File | Where-Object { $_.Name -match '^(Cthangover\.(dll|deps.json|runtimeconfig.json)|cthangover\.(exe|pck)|Godot.SourceGenerators.dll)$' } | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $out 'release-hashes.json')
    & python -X utf8 authoring3d/probes/run_matrix.py --godot (Join-Path $release 'cthangover.exe') --project $release --fixtures '../art_sources/cthangover/asset3d_probe/fixture' --external '../mind_of_pirates/assets' --out $out --runtime export-release
    $matrixExit = $LASTEXITCODE
} finally {
    if (Test-Path $privateMod) { Remove-Item -Recurse -Force $privateMod }
    $privateRoot = Join-Path $release 'mods_private'
    if ((Test-Path $privateRoot) -and !(Get-ChildItem $privateRoot -Force)) { Remove-Item $privateRoot }
}
exit $matrixExit
