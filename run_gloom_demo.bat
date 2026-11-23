@echo off
setlocal
cd /d "%~dp0"
dotnet build Cthangover.csproj --nologo -v:q
if errorlevel 1 exit /b 1
if "%GODOT_BIN%"=="" set GODOT_BIN=godot.exe
start "" /wait "%GODOT_BIN%" --path "%~dp0." -- --game=gloom_demo --new --itest=gloom_demo
set GLOOM_DEMO_EXIT=%ERRORLEVEL%
pause
exit /b %GLOOM_DEMO_EXIT%
