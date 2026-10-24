@echo off
rem Builds site_github\ from site\ (the GitHub Pages copy). Windows wrapper for build_site.sh; needs WSL.
rem Usage: build_site.bat [--url=...] [--repo=...] [--out=...] [--no-push] [--push-url=...]
rem Defaults: build and publish to cthangover.github.io; --no-push builds only.
setlocal EnableExtensions
pushd "%~dp0"
set "SITE_FWD=%CD:\=/%"
set "SITE_WSL="
for /f "delims=" %%p in ('wsl wslpath -a "%SITE_FWD%" 2^>nul') do set "SITE_WSL=%%p"
if not defined SITE_WSL (
    echo wsl is not available: run build_site.sh from a Linux/WSL shell instead.
    popd
    exit /b 1
)
wsl -e bash -lc "cd '%SITE_WSL%' && bash ./build_site.sh %*"
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%