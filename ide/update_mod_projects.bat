@echo off
where python >nul 2>nul
if errorlevel 1 goto use_py
python "%~dp0..\authoring3d\ide\gen_mod_projects.py" %*
exit /b %errorlevel%
:use_py
py -3 "%~dp0..\authoring3d\ide\gen_mod_projects.py" %*
exit /b %errorlevel%
