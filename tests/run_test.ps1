param(
    [ValidateSet("mod", "battle", "menu", "scene", "actioncontext", "switcher")]
    [string]$Test = "mod",
    [string]$Scene = "",
    [string]$LogFile = "",
    [string]$Choices = "",
    [int]$Timeout = 0,
    [switch]$Help
)

if ($Help) {
    @"
Использование: .\run_test.ps1 [-Test <name>] [-Scene <path>] [-LogFile <path>] [-Choices <list>] [-Timeout <sec>]

Параметры:
   -Test       Тип теста: mod, battle, menu, scene, actioncontext, switcher (по умолч. mod)
   -Scene      Целевая сцена или имя логической сцены (для -Test scene и -Test switcher)
   -LogFile    Путь к файлу лога
   -Choices    Индексы выборов в диалогах через запятую
   -Timeout    Таймаут в секундах (только для -Test switcher)

Примеры:
   .\tests\run_test.ps1                                          # Тесты модов
   .\tests\run_test.ps1 -Test battle                             # Тест боя
   .\tests\run_test.ps1 -Test menu                               # Тест главного меню
   .\tests\run_test.ps1 -Test scene -Scene "res://scenes/battle/battle.tscn" -Choices "0,1"
   .\tests\run_test.ps1 -Test switcher -Scene "home_kitchen" -Choices "0" -Timeout 30
"@
    exit
}

$godotPath = "godot"
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path

$testScenes = @{
    mod            = "res://tests/cthangover.Tests.Integration/Scenes/ModTest.tscn"
    battle         = "res://tests/cthangover.Tests.Integration/Scenes/BattleTestScene.tscn"
    menu           = "res://tests/cthangover.Tests.Integration/Scenes/MainMenuTestScene.tscn"
    actioncontext  = "res://tests/cthangover.Tests.Integration/Scenes/GDActionContextCoreTest.tscn"
    switcher       = "res://scenes/test/scene_switcher.tscn"
}

$targetScene = if ($Test -eq "scene") { $Scene } else { $testScenes[$Test] }

if (-not $targetScene) {
    Write-Host "Ошибка: укажите -Scene при -Test scene" -ForegroundColor Red
    exit 1
}

$argsList = @("--path", $projectRoot, $targetScene)

if ($Test -eq "switcher") {
    if (-not $Scene) {
        Write-Host "Ошибка: укажите -Scene при -Test switcher" -ForegroundColor Red
        exit 1
    }
    $argsList += "--scene=$Scene"
    if ($Choices) { $argsList += "--choices=$Choices" }
    if ($Timeout -gt 0) { $argsList += "--timeout=$Timeout" }
}
else {
    if ($Choices) { $argsList += "--choices=$Choices" }
}

if ($LogFile) {
    $argsList += "--log-file=$LogFile"
}

Write-Host "Тест: $Test" -ForegroundColor Cyan
Write-Host "Сцена: $targetScene" -ForegroundColor Cyan
if ($Test -eq "switcher") { Write-Host "Цель: $Scene" -ForegroundColor Cyan }
if ($Choices) { Write-Host "Выборы: $Choices" -ForegroundColor Gray }
if ($Timeout -gt 0 -and $Test -eq "switcher") { Write-Host "Таймаут: ${Timeout}с" -ForegroundColor Gray }
if ($LogFile) { Write-Host "Лог: $LogFile" -ForegroundColor Gray }

& $godotPath $argsList 2>&1 | ForEach-Object { Write-Host $_ }

pause
