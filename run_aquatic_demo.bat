@echo off
setlocal
cd /d "%~dp0"
dotnet build Cthangover.csproj --nologo -v:q
if errorlevel 1 exit /b 1
if "%GODOT_BIN%"=="" if exist "C:\godot\godot.exe" set GODOT_BIN=C:\godot\godot.exe
if "%GODOT_BIN%"=="" set GODOT_BIN=godot.exe
start "" /wait "%GODOT_BIN%" --path "%~dp0." -- --game=aquatic_demo --new --itest=aquatic_plants
set AQUATIC_DEMO_EXIT=%ERRORLEVEL%
pause
exit /b %AQUATIC_DEMO_EXIT%
