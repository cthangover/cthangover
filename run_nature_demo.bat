@echo off
setlocal
cd /d "%~dp0"
dotnet build Cthangover.csproj --nologo -v:q
if errorlevel 1 exit /b 1
if "%GODOT_BIN%"=="" if exist "C:\godot\godot.exe" set GODOT_BIN=C:\godot\godot.exe
if "%GODOT_BIN%"=="" set GODOT_BIN=godot.exe
start "" /wait "%GODOT_BIN%" --path "%~dp0." -- --game=nature_biomes_demo --new %*
set NATURE_DEMO_EXIT=%ERRORLEVEL%
pause
exit /b %NATURE_DEMO_EXIT%
