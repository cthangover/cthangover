#!/usr/bin/env bash
# Full sync of the mods with Gitea and GitHub: repositories, code, releases, catalogs.
# Usage: ./sync_mods.sh [--yes] [--only=a,b] [--skip=a,b] [--delete] [--github-batch=8] [--github-pause=30]
set -euo pipefail
cd "$(dirname "$0")"
if command -v python3 >/dev/null 2>&1; then
    PY=python3
elif command -v python >/dev/null 2>&1; then
    PY=python
else
    echo "Python 3 is required: https://www.python.org/downloads/" >&2
    exit 1
fi
exec "$PY" sync_mods.py "$@"
