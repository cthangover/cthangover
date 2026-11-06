@echo off
setlocal
rem The generators also refresh the statistics and footer counts in HTML.
pushd "%~dp0" || exit /b 1
for %%S in (build-docs-index.py build-quickstart-index.py build-mods-catalog.py build-sitemap.py build-ai-page.py build-seller.py build-page-i18n.py) do (
    python "%%S"
    if errorlevel 1 goto failed
)
popd
exit /b 0

:failed
popd
exit /b 1
