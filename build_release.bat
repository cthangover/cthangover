@echo off
rem ---------------------------------------------------------------------------
rem Build release packages from the export presets in export_presets.cfg.
rem Linux/WSL twin: build_release.sh (same layout, same checks).
rem
rem Usage:
rem   build_release.bat                  both platforms
rem   build_release.bat windows          only Windows
rem   build_release.bat linux            only Linux
rem   build_release.bat --no-archive     folders only, no .zip/.tar.gz
rem   build_release.bat --verify         after the build, play the Windows
rem                                      release up to the prologue
rem
rem Output: releases\<platform>\ (ready-to-run game dir) and
rem         releases\cthangover-<version>-<platform>.zip / .tar.gz
rem
rem The pck carries only engine resources; mods\, collections\ and config\ are
rem copied loose next to the executable (the runtime mod pipeline reads them
rem from the game dir). Dev-only parts of the mods (git repos, GUI test suites,
rem native sources, debug symbols, the other platform's natives) are left out.
rem
rem The Linux .tar.gz is packed through WSL when it is available, so the
rem executable keeps its +x bit; Windows tar.exe cannot store it, then the
rem player has to run `chmod +x cthangover.x86_64` once.
rem
rem --verify unpacks the Windows release into %%TEMP%%, adds the menu_base GUI
rem test suites and runs `--frontend --itest=new_game` from another working
rem dir with the NuGet cache hidden (like a player's machine): menu - Play -
rem cthangover - New game - prolog_alley with Marao's dialog. A window opens
rem and the test drives it on its own; do not click into it.
rem
rem Needs Godot 4.6.3 mono (%%GODOT_BIN%% or godot on PATH) with the
rem 4.6.3.stable.mono export templates, Windows 10+ (tar.exe, robocopy).
rem ---------------------------------------------------------------------------

chcp 65001 >nul
setlocal EnableExtensions

if "%GODOT_BIN%"=="" set GODOT_BIN=godot

set DO_WINDOWS=0
set DO_LINUX=0
set ARCHIVE=1
set VERIFY=0

:parse
if "%~1"=="" goto :parsed
if /i "%~1"=="windows"      (set DO_WINDOWS=1& shift & goto :parse)
if /i "%~1"=="linux"        (set DO_LINUX=1& shift & goto :parse)
if /i "%~1"=="--no-archive" (set ARCHIVE=0& shift & goto :parse)
if /i "%~1"=="--verify"     (set VERIFY=1& shift & goto :parse)
echo Unknown argument: %~1
exit /b 2

:parsed
if "%DO_WINDOWS%%DO_LINUX%"=="00" (set DO_WINDOWS=1& set DO_LINUX=1)

pushd "%~dp0"

for /f "tokens=2 delims==" %%v in ('findstr /b /c:"config/version=" project.godot') do set VERSION=%%~v
if "%VERSION%"=="" (
    echo Cannot read config/version from project.godot.
    goto :fail
)

rem Without it Godot scans earlier builds as project files and packs them.
if not exist releases mkdir releases
type nul > releases\.gdignore

if "%DO_WINDOWS%"=="1" (
    call :build windows "Windows Desktop" cthangover.exe
    if errorlevel 1 goto :fail
)
if "%DO_LINUX%"=="1" (
    call :build linux "Linux" cthangover.x86_64
    if errorlevel 1 goto :fail
)
echo done: version %VERSION%

if "%VERIFY%"=="1" (
    if "%DO_WINDOWS%"=="1" (
        call :verify
        if errorlevel 1 goto :fail
    ) else (
        echo verify: the Linux release is verified by build_release.sh --verify on Linux
    )
)

popd
echo.
echo Release build finished.
exit /b 0

:fail
popd
echo.
echo Release build FAILED - see the output above.
exit /b 1


rem ---------------------------------------------------------------------------
rem :build <platform> <preset> <exe>
rem ---------------------------------------------------------------------------
:build
set PLATFORM=%~1
set PRESET=%~2
set EXE=%~3
set OUT=releases\%PLATFORM%
set NAME=cthangover-%VERSION%-%PLATFORM%

echo === %PLATFORM%: export (%PRESET%) ===
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"
"%GODOT_BIN%" --headless --path . --export-release "%PRESET%" "%OUT%\%EXE%"
if not exist "%OUT%\%EXE%" (
    echo Export of %PLATFORM% produced no %EXE%.
    exit /b 1
)

echo === %PLATFORM%: loose content ===
if "%PLATFORM%"=="windows" (set NATIVE_SKIP=*.so) else (set NATIVE_SKIP=)
call :copy_tree mods "%OUT%\mods" %NATIVE_SKIP%
if errorlevel 1 exit /b 1
call :copy_tree collections "%OUT%\collections"
if errorlevel 1 exit /b 1
call :copy_tree config "%OUT%\config"
if errorlevel 1 exit /b 1
if "%PLATFORM%"=="linux" (
    for /d %%m in ("%OUT%\mods\*") do if exist "%%m\native\*.dll" del /q "%%m\native\*.dll"
)

if "%ARCHIVE%"=="0" exit /b 0
if "%PLATFORM%"=="windows" (
    if exist "releases\%NAME%.zip" del /q "releases\%NAME%.zip"
    call :pack_windows
    if errorlevel 1 exit /b 1
    echo archive: releases\%NAME%.zip
    exit /b 0
)
call :pack_linux
exit /b %ERRORLEVEL%


rem ---------------------------------------------------------------------------
rem :copy_tree <src> <dst> [extra file masks to skip]
rem robocopy exit codes below 8 mean success.
rem ---------------------------------------------------------------------------
:copy_tree
robocopy "%~1" "%~2" /E /XD .git .git-template test_suites source_code /XF *.pdb *.lib *.zip %3 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo Copying %~1 failed.
    exit /b 1
)
exit /b 0


rem ---------------------------------------------------------------------------
rem :pack_windows — releases\<NAME>.zip.
rem Windows tar.exe (bsdtar) has no -s, so the build folder is renamed to the
rem archive root name, packed, and renamed back.
rem ---------------------------------------------------------------------------
:pack_windows
if exist "releases\%NAME%" rmdir /s /q "releases\%NAME%"
ren "releases\windows" "%NAME%"
if errorlevel 1 (
    echo Cannot rename releases\windows to %NAME%.
    exit /b 1
)
tar -a -c -f "releases\%NAME%.zip" -C releases "%NAME%"
set PACK_ERR=%ERRORLEVEL%
ren "releases\%NAME%" "windows"
if not "%PACK_ERR%"=="0" (
    echo Packing releases\%NAME%.zip failed.
    exit /b 1
)
exit /b 0

rem ---------------------------------------------------------------------------
rem :pack_linux — releases\<NAME>.tar.gz, through WSL when possible.
rem ---------------------------------------------------------------------------
:pack_linux
if exist "releases\%NAME%.tar.gz" del /q "releases\%NAME%.tar.gz"
set WSL_RELEASES=
set RELEASES_FWD=%CD:\=/%
where wsl >nul 2>nul
if not errorlevel 1 (
    for /f "delims=" %%p in ('wsl wslpath -a "%RELEASES_FWD%/releases" 2^>nul') do set WSL_RELEASES=%%p
)
if not "%WSL_RELEASES%"=="" (
    wsl chmod +x "%WSL_RELEASES%/linux/cthangover.x86_64"
    wsl tar -C "%WSL_RELEASES%" -czf "%WSL_RELEASES%/%NAME%.tar.gz" --transform "s|^linux|%NAME%|" linux
    if errorlevel 1 exit /b 1
    echo archive: releases\%NAME%.tar.gz
    exit /b 0
)
if exist "releases\%NAME%" rmdir /s /q "releases\%NAME%"
ren "releases\linux" "%NAME%"
if errorlevel 1 (
    echo Cannot rename releases\linux to %NAME%.
    exit /b 1
)
tar -c -z -f "releases\%NAME%.tar.gz" -C releases "%NAME%"
set PACK_ERR=%ERRORLEVEL%
ren "releases\%NAME%" "linux"
if not "%PACK_ERR%"=="0" (
    echo Packing releases\%NAME%.tar.gz failed.
    exit /b 1
)
echo archive: releases\%NAME%.tar.gz
echo WARNING: packed without WSL - after unpacking run: chmod +x cthangover.x86_64
exit /b 0


rem ---------------------------------------------------------------------------
rem :verify — plays the Windows release up to the prologue (see header).
rem ---------------------------------------------------------------------------
:verify
echo === windows: verify (menu - New game - prologue) ===
set VDIR=%TEMP%\cthangover_verify_%RANDOM%%RANDOM%
set VGAME=%VDIR%\game
mkdir "%VGAME%"
if exist "releases\cthangover-%VERSION%-windows.zip" (
    tar -x -f "releases\cthangover-%VERSION%-windows.zip" -C "%VGAME%" --strip-components 1
) else (
    robocopy releases\windows "%VGAME%" /E /NFL /NDL /NJH /NJS /NP >nul
)
robocopy mods\menu_base\test_suites "%VGAME%\mods\menu_base\test_suites" /E /NFL /NDL /NJH /NJS /NP >nul
if not exist "%VGAME%\cthangover.exe" (
    echo verify: the unpacked release has no cthangover.exe
    exit /b 1
)

set VLOG=%APPDATA%\cthangover\logs\cthangover.log
set VFROM=0
if exist "%VLOG%" for /f %%n in ('find /c /v "" ^< "%VLOG%"') do set VFROM=%%n

pushd "%VDIR%"
set NUGET_PACKAGES=%VDIR%\no_nuget
start "" /wait "%VGAME%\cthangover.exe" -- --frontend --itest=new_game
set VEXIT=%ERRORLEVEL%
set NUGET_PACKAGES=
popd

set VRESULT=
for /f "delims=" %%l in ('more +%VFROM% "%VLOG%" ^| findstr /l /c:"name=new_game passed="') do set VRESULT=%%l
more +%VFROM% "%VLOG%" | findstr /l /c:"NewGameTest: [FAIL]"

echo %VRESULT% | findstr /l /c:"failed=0" >nul
if "%VEXIT%"=="0" if not errorlevel 1 (
    echo verify: OK - %VRESULT%
    rmdir /s /q "%VDIR%"
    exit /b 0
)
echo verify: FAILED (exit %VEXIT%) %VRESULT%
echo game log: %VLOG%
echo unpacked release kept in: %VDIR%
exit /b 1
