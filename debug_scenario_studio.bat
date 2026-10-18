@echo off
rem ---------------------------------------------------------------------------
rem Launch of the Scenario Studio prototype (mods\scenario_studio):
rem the game starts in a scene with every mod loaded and the studio dock
rem opens on its own (--studio). Godot logs are printed into this console.
rem
rem Usage:
rem   debug_scenario_studio.bat                    home_kitchen (default)
rem   debug_scenario_studio.bat prolog_alley       any logical scene name
rem   debug_scenario_studio.bat home_kitchen --with-mods=x   extra game flags
rem
rem The dock can be closed with its X and reopened with the toolbar icon
rem "page with a play triangle" (top right of the HUD).
rem ---------------------------------------------------------------------------

chcp 65001 >nul
setlocal

if "%GODOT_BIN%"=="" set GODOT_BIN=godot

set SCENE=%~1
if "%SCENE%"=="" set SCENE=home_kitchen
shift

set EXTRA=
:collect
if "%~1"=="" goto :run
set EXTRA=%EXTRA% %1
shift
goto :collect

:run
pushd "%~dp0"

echo [1/2] Building Cthangover.csproj...
dotnet build Cthangover.csproj
if errorlevel 1 goto :build_failed

echo [2/2] Launching scene '%SCENE%' with the Scenario Studio...
"%GODOT_BIN%" --path . -- --scene=%SCENE% --all-mods --studio%EXTRA%
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
