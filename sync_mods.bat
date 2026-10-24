@echo off
rem Full sync of the mods with Gitea and GitHub: repositories, code, releases, catalogs.
rem Usage: sync_mods.bat [--yes] [--only=a,b] [--skip=a,b] [--delete] [--github-batch=8] [--github-pause=30]
rem No arguments: report, ask once, apply; then wait for a key (double-click friendly).
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
%PY% "%~dp0sync_mods.py" %*
set "RC=%ERRORLEVEL%"
popd
if "%~1"=="" pause
exit /b %RC%
