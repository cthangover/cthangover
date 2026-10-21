#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Full test run: core build -> xUnit unit tests -> mod unit tests (mod_units)
# -> mod GUI test suites (--itest) -> autotest/coverage scenes -> summary.
# Windows twin: run_tests.bat.
#
# Usage:
#   ./run_tests.sh                    everything from tests/tests.manifest.json
#   ./run_tests.sh --test inventory   one test (unit, mod_units, inventory, ...)
#   ./run_tests.sh --list             list the tests
#   ./run_tests.sh --no-build         skip the core build
# Any argument is passed to tests/Cthangover.TestRunner as is.
#
# Windowed tests (live2d_avatar, tools_windows, studio_live, ...) need a
# display: without $DISPLAY the run goes through xvfb-run when it is installed.
#
# Output: summary at the end, exit code 0 only when everything is green.
#         Per-test logs:  tests/artifacts/logs/<name>.log
#         JSON report:    tests/artifacts/test-results.json
# ---------------------------------------------------------------------------
set -uo pipefail

cd "$(dirname "$0")"
ROOT="$(pwd)"

if ! command -v dotnet >/dev/null 2>&1 && [ -f /workspace/tools/env.sh ]; then
    source /workspace/tools/env.sh
fi
if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet not found: install the .NET 8 SDK or source /workspace/tools/env.sh" >&2
    exit 2
fi

REPORT="$ROOT/tests/artifacts/test-results.json"
mkdir -p "$ROOT/tests/artifacts"

RUNNER=(dotnet run --project tests/Cthangover.TestRunner --)
if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null 2>&1; then
    RUNNER=(xvfb-run -a -s "-screen 0 1920x1080x24" "${RUNNER[@]}")
elif [ -z "${DISPLAY:-}" ]; then
    echo "WARNING: no \$DISPLAY and no xvfb-run, windowed tests will fail" >&2
fi

START=$(date +%s)
"${RUNNER[@]}" --json "$REPORT" "$@"
EXIT_CODE=$?
ELAPSED=$(( $(date +%s) - START ))

echo
echo "Total time: $((ELAPSED / 60)) min $((ELAPSED % 60)) s"
echo "Logs:       tests/artifacts/logs/"
[ -f "$REPORT" ] && echo "Report:     tests/artifacts/test-results.json"
if [ $EXIT_CODE -eq 0 ]; then
    echo "RESULT: ALL TESTS PASSED"
else
    echo "RESULT: FAILED (exit code $EXIT_CODE)"
fi
exit $EXIT_CODE
