# Retakes the screenshots of the site (Windows host with a display, cwd = repository root).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File site\make-shots.ps1 [-Python <python with Pillow>]
#
# The game runs windowed through the test suites that take --shots=<dir>: site_shots (novel, locations,
# panels, card battle of the main game), rpg_walk, trade and rpg_plus (RPG world, FF battle, trade window, prologue, sheet), menu_flow
# (game picker) and tools_windows (editors). The player's settings.json gets "Language": "en" for the run
# and is restored afterwards. PNGs land in gen/shots/site; site/shots-to-img.py turns the ones the page uses (and deletes the rest)
# into site/img/shots/*.jpg.
param(
  [string]$Python = "python"
)

$ErrorActionPreference = "Continue"
$godot = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { "godot.exe" }
$shots = Join-Path (Get-Location) "gen\shots\site"
$userDir = Join-Path $env:APPDATA "cthangover"
$settings = Join-Path $userDir "settings.json"
$backup = Join-Path $userDir "settings.json.site-shots-backup"
$code = 0

if (Test-Path $shots) { Remove-Item $shots -Recurse -Force }
New-Item -ItemType Directory -Force $shots | Out-Null
New-Item -ItemType Directory -Force $userDir | Out-Null
$hadSettings = Test-Path $settings
if ($hadSettings) { Copy-Item $settings $backup -Force }

try {
  $json = if ($hadSettings) { Get-Content $settings -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
  $json | Add-Member -NotePropertyName "Language" -NotePropertyValue "en" -Force
  $json | ConvertTo-Json -Depth 5 | Set-Content $settings -Encoding UTF8

  $runs = @(
    @("site_shots", "--game=cthangover", "--new", "--itest=site_shots"),
    @("rpg_walk", "--game=rpg_plus", "--new", "--itest=rpg_walk"),
    @("trade", "--game=rpg_plus", "--new", "--itest=trade"),
    @("rpg_plus", "--game=rpg_plus", "--new", "--itest=rpg_plus"),
    @("menu_flow", "--frontend", "--itest=menu_flow"),
    @("tools_windows", "--scene=home_kitchen", "--itest=tools_windows")
  )
  foreach ($run in $runs) {
    $name = $run[0]
    $dir = Join-Path $shots $name
    $userArgs = $run[1..($run.Length - 1)] + "--shots=$dir"
    Write-Output "== $name"
    & $godot --path . res://scenes/menu/launcher.tscn -- @userArgs 2>&1 | Select-String "TEST_RESULT|\[FAIL\]|error CS|MODS_COMPILE.*rror" | Select-Object -Last 15
    if ($LASTEXITCODE -ne 0) { $code = 1 }
    if (Test-Path $dir) { Get-ChildItem $dir -Filter *.png | ForEach-Object { "  " + $_.Name } }
  }
}
finally {
  if ($hadSettings) { Move-Item $backup $settings -Force } else { Remove-Item $settings -Force -ErrorAction SilentlyContinue }
}

& $Python site\shots-to-img.py $shots site\img\shots
if ($LASTEXITCODE -ne 0) { $code = 1 }
exit $code
