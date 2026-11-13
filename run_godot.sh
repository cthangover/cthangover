#!/usr/bin/env bash
# Launch the net10.0 project with the installed .NET 10 toolchain.
set -euo pipefail
exec "${GODOT_BIN:-godot}" --path "$(cd "$(dirname "$0")" && pwd)" "$@"
