$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot '../authoring3d/ide/gen_mod_projects.py'
if (Get-Command python -ErrorAction SilentlyContinue) {
    & python $generator @args
} else {
    & py -3 $generator @args
}
exit $LASTEXITCODE
