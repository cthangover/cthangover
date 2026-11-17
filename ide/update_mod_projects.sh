#!/usr/bin/env sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
exec python3 "$task_root/authoring3d/ide/gen_mod_projects.py" "$@"
