@echo off
rem ---------------------------------------------------------------------------
rem Commit and push every mod repository under mods\ (each mod with its own
rem .git, remote git.cthangover.ru/cthangover/mod_<name>).
rem
rem Usage:
rem   push_mods.bat                          commit with the default message, push
rem   push_mods.bat -m "Fix dialogs"         commit message
rem   push_mods.bat --only cooking,tools     only these mods
rem   push_mods.bat --dry-run                show what would be done, change nothing
rem   push_mods.bat --no-commit              push existing commits only
rem
rem Logic: push_mods.py
rem ---------------------------------------------------------------------------

chcp 65001 >nul
setlocal

pushd "%~dp0"
python push_mods.py %*
set EXIT_CODE=%ERRORLEVEL%
popd

echo.
if "%EXIT_CODE%"=="0" (echo All mods pushed.) else (echo Some mods failed - see the output above.)
pause
exit /b %EXIT_CODE%
