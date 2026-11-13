@echo off
rem ---------------------------------------------------------------------------
rem Full test run: build once, then parallel xUnit/mod/scene launches -> summary.
rem --jobs N limits all children; --gui-jobs 2 limits windows; --write-jobs 2 limits writable copies.
rem --jobs 1 uses the sequential copy/restore protocol.
rem Linux/WSL twin: run_tests.sh.
rem
rem Usage:
rem   run_tests.bat                    everything from tests\tests.manifest.json
rem   run_tests.bat --test inventory   one test (unit, mod_units, inventory, ...)
rem   run_tests.bat --list             list the tests
rem   run_tests.bat --no-build         skip the core build
rem Any argument is passed to tests\Cthangover.TestRunner as is.
rem
rem Godot is taken from GODOT_BIN or `godot` on PATH.
rem
rem Output: summary at the end, exit code 0 only when everything is green.
rem         Per-test logs:  tests\artifacts\runs\<id>\tests\<name>\process.log
rem         JSON report:    tests\artifacts\test-results.json
rem ---------------------------------------------------------------------------

chcp 65001 >nul
setlocal

pushd "%~dp0"

set REPORT=%~dp0tests\artifacts\test-results.json
if not exist "%~dp0tests\artifacts" mkdir "%~dp0tests\artifacts"

set START=%TIME%
dotnet run --project tests\Cthangover.TestRunner -- --json "%REPORT%" %*
set EXIT_CODE=%ERRORLEVEL%

popd
echo.
echo Started:    %START%
echo Finished:   %TIME%
echo Runs:       tests\artifacts\runs\
if exist "%REPORT%" echo Report:     tests\artifacts\test-results.json
if "%EXIT_CODE%"=="0" (
    echo RESULT: ALL TESTS PASSED
) else (
    echo RESULT: FAILED ^(exit code %EXIT_CODE%^)
)

rem Keep the window open when started by double click from Explorer.
echo %cmdcmdline% | find /i "%~nx0" >nul && pause
exit /b %EXIT_CODE%
