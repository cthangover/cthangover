@echo off
rem Build and publish the GitHub Pages copy; --no-push builds only.
setlocal EnableExtensions
pushd "%~dp0"
set "SITE_LOG=%CD%\build_site.log"
set "SITE_FWD=%CD:\=/%"
set "SITE_WSL="
set "RC=1"
> "%SITE_LOG%" echo cthangover GitHub Pages build
for /f "delims=" %%p in ('wsl wslpath -a "%SITE_FWD%" 2^>nul') do set "SITE_WSL=%%p"
if not defined SITE_WSL (
    >> "%SITE_LOG%" echo WSL is not available: run build_site.sh from a Linux/WSL shell instead.
    goto finish
)
echo Building site. Full log: "%SITE_LOG%"
wsl -e bash -lc "cd '%SITE_WSL%' && bash ./build_site.sh %*" >> "%SITE_LOG%" 2>&1
set "RC=%ERRORLEVEL%"
:finish
type "%SITE_LOG%"
if not "%RC%"=="0" (
    echo.
    echo Site build failed with exit code %RC%. Log: "%SITE_LOG%"
    if not defined CI if not "%SITE_BUILD_NO_PAUSE%"=="1" pause
)
popd
exit /b %RC%
