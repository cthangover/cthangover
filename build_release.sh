#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Build release packages from the export presets in export_presets.cfg.
# Windows twin: build_release.bat.
#
# Usage:
#   ./build_release.sh                 both platforms
#   ./build_release.sh linux           only Linux
#   ./build_release.sh windows         only Windows
#   ./build_release.sh --no-archive    folders only, no .tar.gz/.zip
#   ./build_release.sh --verify        after the build, play the Linux release
#                                      up to the prologue (needs a display or
#                                      xvfb-run)
#
# Output: releases/<platform>/ (ready-to-run game dir) and
#         releases/cthangover-<version>-<platform>.{tar.gz,zip}
#
# The pck carries only engine resources; mods/, collections/ and config/ are
# copied loose next to the executable, because the runtime mod pipeline reads
# them from the game dir. Dev-only parts of the mods (git repos, GUI test
# suites, native sources, debug symbols, the other platform's natives) are
# left out. Fixture mods named test_* are excluded; stat_checks is gameplay.
#
# --verify unpacks the release into a temp dir, adds the menu_base GUI test
# suites and runs `--frontend --itest=new_game` from another working dir with
# the NuGet cache hidden (like a player's machine): menu -> Play -> cthangover
# -> New game -> prolog_alley with Marao's dialog. Exit code 1 when it fails.
#
# Needs Godot 4.6.3 mono ($GODOT_BIN or `godot` on PATH, see
# /workspace/tools/env.sh) with the 4.6.3.stable.mono export templates.
# ---------------------------------------------------------------------------
set -euo pipefail

cd "$(dirname "$0")"

GODOT="${GODOT_BIN:-godot}"
VERSION="$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)"
VERIFY_TIMEOUT=900
ARCHIVE=1
VERIFY=0
PLATFORMS=()
for arg in "$@"; do
    case "$arg" in
        linux|windows) PLATFORMS+=("$arg") ;;
        --no-archive)  ARCHIVE=0 ;;
        --verify)      VERIFY=1 ;;
        *) echo "unknown argument: $arg" >&2; exit 2 ;;
    esac
done
[ ${#PLATFORMS[@]} -eq 0 ] && PLATFORMS=(windows linux)

dotnet build mods/debug_client/source_code/DebugClient.Mcp.csproj -c Release -p:RestoreLockedMode=true --nologo

# Without it Godot scans earlier builds as project files and packs them.
mkdir -p releases
touch releases/.gdignore

# Copy a loose content tree into the release, dropping dev-only files.
copy_tree() {
    local src="$1" dst="$2"; shift 2
    mkdir -p "$dst"
    tar -C "$src" -cf - \
        --exclude='.git' --exclude='.git-template' --exclude='test_suites' \
        --exclude='source_code' --exclude='*.pdb' --exclude='*.lib' \
        --exclude='./*.zip' "$@" . \
        | tar -C "$dst" -xf -
}

build() {
    local platform="$1" preset exe natives_skip
    case "$platform" in
        windows) preset="Windows Desktop"; exe="cthangover.exe";     natives_skip='*.so' ;;
        linux)   preset="Linux";           exe="cthangover.x86_64"; natives_skip='native/*.dll' ;;
    esac
    local out="releases/$platform"

    echo "=== $platform: export ($preset) ==="
    rm -rf "$out"
    mkdir -p "$out"
    "$GODOT" --headless --path . --export-release "$preset" "$out/$exe"
    [ -f "$out/$exe" ] || { echo "export of $platform produced no $exe" >&2; exit 1; }

    local rid=linux-x64
    [ "$platform" != windows ] || rid=win-x64

    echo "=== $platform: loose content ==="
    # test_* is reserved for fixture mods; stat_checks is runtime gameplay.
    copy_tree mods        "$out/mods" --exclude='./test_*' --exclude="$natives_skip"
    copy_tree collections "$out/collections"
    copy_tree config      "$out/config"
    copy_tree DebugClient "$out/DebugClient" --exclude='__pycache__'
    "${PYTHON_BIN:-python3}" tests/verify_release_runtime.py "$out" "$rid"

    if [ "$ARCHIVE" -eq 1 ]; then
        local name="cthangover-$VERSION-$platform"
        if [ "$platform" = linux ]; then
            tar -C releases -czf "releases/$name.tar.gz" --transform "s|^linux|$name|" linux
            echo "archive: releases/$name.tar.gz"
        elif command -v zip >/dev/null; then
            # The archive root is named through a symlink in a temp dir:
            # releases/<name> may be the user's own unpacked copy.
            local tmp
            tmp="$(mktemp -d)"
            ln -s "$PWD/releases/windows" "$tmp/$name"
            rm -f "releases/$name.zip"
            (cd "$tmp" && zip -qr "$OLDPWD/releases/$name.zip" "$name")
            rm -rf "$tmp"
            echo "archive: releases/$name.zip"
        else
            echo "zip not found: releases/windows left as a folder"
        fi
    fi
}

# Plays the Linux release like a player up to the prologue (see header).
verify_linux() {
    echo "=== linux: verify (menu -> New game -> prologue) ==="
    local dir game log from status
    dir="$(mktemp -d)"
    # EXIT also covers verifier/display failures; signals terminate through the same cleanup.
    trap "cleanup_verify_linux $(printf '%q' "$dir")" EXIT
    trap 'exit 130' INT
    trap 'exit 143' TERM
    game="$dir/game"
    mkdir -p "$game"
    # --no-archive must never select a leftover archive from an earlier build.
    if [ "$ARCHIVE" -eq 1 ]; then
        echo "verify source: archive created by this build"
        tar -xzf "releases/cthangover-$VERSION-linux.tar.gz" -C "$game" --strip-components=1
    else
        echo "verify source: freshly exported releases/linux folder"
        cp -r releases/linux/. "$game"
    fi
    "${PYTHON_BIN:-python3}" tests/verify_release_runtime.py "$game" linux-x64 --expected-release releases/linux
    cp -r mods/menu_base/test_suites "$game/mods/menu_base/"

    log="${XDG_DATA_HOME:-$HOME/.local/share}/cthangover/logs/cthangover.log"
    from=$(( $( [ -f "$log" ] && wc -l < "$log" || echo 0 ) + 1 ))

    local runner=()
    if [ -z "${DISPLAY:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
        command -v xvfb-run >/dev/null || { echo "no display and no xvfb-run: cannot verify" >&2; exit 1; }
        runner=(xvfb-run -a -s "-screen 0 1920x1080x24")
    fi

    set +e
    (cd "$dir" && NUGET_PACKAGES="$dir/no_nuget" timeout "$VERIFY_TIMEOUT" \
        "${runner[@]}" "$game/cthangover.x86_64" -- --frontend --itest=new_game > "$dir/stdout.log" 2>&1)
    status=$?
    set -e

    local result
    result="$(tail -n +"$from" "$log" 2>/dev/null | grep -a '\[TEST_RESULT\] name=new_game' | tail -1 || true)"
    tail -n +"$from" "$log" 2>/dev/null | grep -a 'NewGameTest: \[FAIL\]' || true
    if [ "$status" -eq 0 ] && [[ "$result" == *"failed=0"* ]]; then
        echo "verify: OK — ${result#*\] }"
        rm -rf "$dir"
        trap - EXIT INT TERM
    else
        echo "verify: FAILED (exit $status) ${result:-no [TEST_RESULT] line}" >&2
        echo "game log: $log, stdout: $dir/stdout.log" >&2
        exit 1
    fi
}

cleanup_verify_linux() {
    # EXIT can run after verify_linux's local variables have left scope.
    local cleanup_dir="$1"
    if [ -d "$cleanup_dir" ]; then
        mkdir -p releases/verify-logs || true
        cp "$cleanup_dir/stdout.log" releases/verify-logs/linux-stdout.log 2>/dev/null || true
        rm -rf "$cleanup_dir"
    fi
}

for p in "${PLATFORMS[@]}"; do build "$p"; done
echo "done: ${PLATFORMS[*]} (version $VERSION)"

if [ "$VERIFY" -eq 1 ]; then
    if [[ " ${PLATFORMS[*]} " == *" linux "* ]]; then
        verify_linux
    else
        echo "verify: the Windows release is verified by build_release.bat --verify on Windows"
    fi
fi
