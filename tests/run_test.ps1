param(
    [ValidateSet("mod", "battle", "menu", "scene")]
    [string]$Test = "mod",
    [string]$Scene = "",
    [string]$LogFile = "",
    [string]$Choices = "",
    [switch]$Help
)

if ($Help) {
    @"
Использование: .\run_test.ps1 [-Test <name>] [-Scene <path>] [-LogFile <path>] [-Choices <list>]

Параметры:
  -Test       Тип теста: mod, battle, menu, scene (по умолч. mod)
  -Scene      Произвольная сцена для прогона (только с -Test scene)
  -LogFile    Путь к файлу лога
  -Choices    Индексы выборов в диалогах через запятую

Примеры:
  .\tests\run_test.ps1                                          # Тесты модов
  .\tests\run_test.ps1 -Test battle                             # Тест боя
  .\tests\run_test.ps1 -Test menu                               # Тест главного меню
  .\tests\run_test.ps1 -Test scene -Scene "res://Scenes/Battle.tscn" -Choices "0,1"
"@
    exit
}

$godotPath = "godot"
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path

$testScenes = @{
    mod    = "res://tests/cthangover.Tests.Integration/Scenes/ModTest.tscn"
    battle = "res://tests/cthangover.Tests.Integration/Scenes/BattleTestScene.tscn"
    menu   = "res://tests/cthangover.Tests.Integration/Scenes/MainMenuTestScene.tscn"
}

$targetScene = if ($Test -eq "scene") { $Scene } else { $testScenes[$Test] }

if (-not $targetScene) {
    Write-Host "Ошибка: укажите -Scene при -Test scene" -ForegroundColor Red
    exit 1
}

$argsList = @("--path", $projectRoot, $targetScene)
if ($LogFile) {
    $argsList += "--log-file=$LogFile"
}
if ($Choices) {
    $argsList += "--choices=$Choices"
}

Write-Host "Тест: $Test" -ForegroundColor Cyan
Write-Host "Сцена: $targetScene" -ForegroundColor Cyan
if ($Choices) { Write-Host "Выборы: $Choices" -ForegroundColor Gray }
if ($LogFile) { Write-Host "Лог: $LogFile" -ForegroundColor Gray }

& $godotPath $argsList 2>&1 | ForEach-Object { Write-Host $_ }

pause
