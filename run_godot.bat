@echo off
rem Launch the net10.0 project with the installed .NET 10 toolchain.
setlocal
if "%GODOT_BIN%"=="" set GODOT_BIN=godot
"%GODOT_BIN%" --path "%~dp0." %*
exit /b %ERRORLEVEL%
