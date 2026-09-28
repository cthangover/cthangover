@echo off
rem ---------------------------------------------------------------------------
rem Debug launch of the card battle interface:
rem   party   - Marao + Murakami
rem   enemies - wolf_1, wolf_2, wolf_3
rem No auto-clicking: the battle is played by hand, dialogs and turns are
rem untouched. Godot logs are printed into this console window.
rem
rem Usage:
rem   debug_card_battle.bat                build the core project, then launch
rem   debug_card_battle.bat --verbose      extra flags are passed to Godot
rem
rem Encounter definition:
rem   tests\Cthangover.Tests.Integration\Battle\ManualCardBattleSetup.cs
rem Battle UI code (mods are recompiled by the game itself on every launch):
rem   mods\card_battle\...
rem ---------------------------------------------------------------------------

chcp 65001 >nul
setlocal

if "%GODOT_BIN%"=="" set GODOT_BIN=godot

pushd "%~dp0"

echo [1/2] Building Cthangover.csproj...
dotnet build Cthangover.csproj
if errorlevel 1 goto :build_failed

echo [2/2] Launching card battle (Marao + Murakami vs 3 wolves)...
"%GODOT_BIN%" --path . "tests/Cthangover.Tests.Integration/Scenes/manual_card_battle_scene.tscn" %*
set EXIT_CODE=%ERRORLEVEL%

popd
echo.
echo Godot exited with code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%

:build_failed
popd
echo.
echo Build failed - fix the compiler errors above and run again.
pause
exit /b 1
