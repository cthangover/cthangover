#!/usr/bin/env bash
# Builds a release archive of every mod and the catalog (Gitea + GitHub mirrors) into releases/.
# Usage: ./build_mod_releases.sh [--only=a,b] [--skip=a,b] [--hosts=gitea,github] [--out=dir]
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
exec "$PY" build_mod_releases.py "$@"
