@echo off
rem Builds a release archive of every mod and the Gitea/GitHub catalogs into releases\.
rem Usage: build_mod_releases.bat [--only=a,b] [--skip=a,b] [--hosts=gitea,github] [--out=dir]
rem No arguments: all mods, then wait for a key (double-click friendly).
setlocal EnableExtensions
pushd "%~dp0"
set "PY="
where python >nul 2>nul
if not errorlevel 1 set "PY=python"
if not defined PY (
    where py >nul 2>nul
    if not errorlevel 1 set "PY=py -3"
)
if not defined PY (
    echo Python 3 is required: https://www.python.org/downloads/
    popd
    exit /b 1
)
%PY% "%~dp0build_mod_releases.py" %*
set "RC=%ERRORLEVEL%"
popd
if "%~1"=="" pause
exit /b %RC%
