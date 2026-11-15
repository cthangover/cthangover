#!/usr/bin/env python3
"""Check explicit C# paths with Roslyn syntax; never invoke version-control commands."""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
from typing import Iterator

TOOL_DIR = Path(__file__).resolve().parent
DEFAULT_ROOT = TOOL_DIR.parents[1]
SKIP_DIRS = {".git", ".git-template", ".godot", "bin", "obj", "node_modules", "__pycache__"}


def tool_environment() -> dict[str, str]:
    """The container lacks ICU; CLI parsing does not require culture-specific behavior."""
    environment = dict(os.environ)
    if sys.platform.startswith("linux"):
        environment.setdefault("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1")
    return environment


def dotnet_executable() -> str:
    """Use the installed SDK, including this workspace's container toolchain."""
    executable = shutil.which("dotnet")
    if executable:
        return executable
    fallback = DEFAULT_ROOT.parent / "tools" / "dotnet10" / "dotnet"
    if fallback.is_file():
        return str(fallback)
    raise RuntimeError(".NET SDK 10 is required; load the workspace tool environment first.")


@contextmanager
def cache_lease(path: Path) -> Iterator[None]:
    """Serialize cache publication across CLI callers; the OS releases the lease after a crash."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a+b") as handle:
        handle.seek(0, os.SEEK_END)
        if handle.tell() == 0:
            handle.write(b"0")
            handle.flush()
        started = time.monotonic()
        while True:
            try:
                handle.seek(0)
                if os.name == "nt":
                    import msvcrt
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except OSError:
                if time.monotonic() - started > 90:
                    raise RuntimeError("Timed out waiting for the style-parser build lease.")
                time.sleep(0.05)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def parser_command() -> list[str]:
    """Build one syntax-only helper per source/SDK signature outside every repository."""
    executable = dotnet_executable()
    version = subprocess.run(
        [executable, "--version"], capture_output=True, text=True, check=True, timeout=15, env=tool_environment(),
    ).stdout.strip()
    if not version.startswith("10."):
        raise RuntimeError(f"The style parser requires SDK 10; found {version}.")
    digest = hashlib.sha256(version.encode("utf-8"))
    sources = [TOOL_DIR / "CodeStyle.Parser.csproj", *sorted((TOOL_DIR / "parser").glob("*.cs.txt"))]
    for source in sources:
        digest.update(source.name.encode("utf-8"))
        digest.update(source.read_bytes())
    cache = Path(tempfile.gettempdir()) / "cthangover-code-style" / digest.hexdigest()[:24]
    assembly = cache / "bin" / "CodeStyle.Parser.dll"
    with cache_lease(cache / "build.lock"):
        if not assembly.is_file():
            intermediate = cache / "obj"
            command = [
                executable, "build", str(TOOL_DIR / "CodeStyle.Parser.csproj"),
                "--nologo", "--configuration", "Release", "--output", str(cache / "bin"),
                "-p:BaseIntermediateOutputPath=" + str(intermediate) + os.sep,
                "-p:MSBuildProjectExtensionsPath=" + str(intermediate) + os.sep,
            ]
            completed = subprocess.run(command, capture_output=True, text=True, timeout=90, env=tool_environment())
            if completed.returncode:
                raise RuntimeError("Style-parser build failed:\n" + completed.stdout + completed.stderr)
            if not assembly.is_file():
                raise RuntimeError("Style-parser build produced no assembly.")
    return [executable, str(assembly)]


def analyze_sources(sources: list[dict], max_line_length: int = 140) -> list[dict]:
    """Analyze an explicit batch, including non-reporting ancestry context; returns real syntax findings."""
    if max_line_length < 40:
        raise ValueError("The line limit must be at least 40 columns.")
    request = {"sources": sources, "maxLineLength": max_line_length}
    completed = subprocess.run(
        parser_command(), input=json.dumps(request, ensure_ascii=False),
        capture_output=True, text=True, encoding="utf-8", timeout=90, env=tool_environment(),
    )
    if completed.returncode:
        raise RuntimeError(completed.stderr.strip() or "Style parser failed without diagnostics.")
    result = json.loads(completed.stdout)
    if not isinstance(result, list):
        raise RuntimeError("Style parser returned an invalid report.")
    return result


def is_csharp(path: Path) -> bool:
    """The helper templates use .cs.txt so the game's default compile glob cannot absorb authoring code."""
    return path.name.endswith((".cs", ".cs.txt"))


def source_files(path: Path) -> Iterator[Path]:
    """Walk only source files and ignore repositories, build products and vendor package directories."""
    if path.is_file():
        if is_csharp(path):
            yield path
        return
    for directory, folders, files in os.walk(path):
        folders[:] = sorted(folder for folder in folders if folder not in SKIP_DIRS)
        for name in sorted(files):
            candidate = Path(directory) / name
            if is_csharp(candidate):
                yield candidate


def owning_mod(path: Path, root: Path) -> Path | None:
    """Return the top-level public/private mod; nested suites still belong to that production owner."""
    relative = path.relative_to(root)
    if len(relative.parts) >= 3 and relative.parts[0] in {"mods", "mods_private"}:
        return root / relative.parts[0] / relative.parts[1]
    return None


def read_manifest(mod: Path) -> dict:
    """Manifest errors are tool failures instead of silently disabling GUI checks."""
    path = mod / "manifest.json"
    if not path.is_file():
        return {}
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError("Invalid mod manifest: " + str(path))
    return data


def production_prefab_exists(mod: Path) -> bool:
    """Ignore inactive suites: their screenshots/fixtures cannot satisfy a gameplay screen's prefab requirement."""
    for directory, folders, files in os.walk(mod):
        folders[:] = [folder for folder in folders if folder not in SKIP_DIRS | {"tests", "test_suites"}]
        if any(name.endswith(".tscn") for name in files):
            return True
    return False


def source_policy_manifest(path: Path, mod: Path, fallback: dict) -> dict:
    """An optional editor integration keeps its own utility role inside a runtime host mod."""
    directory = path.parent
    while directory != mod and mod in directory.parents:
        if (directory / "manifest.json").is_file():
            return read_manifest(directory)
        directory = directory.parent
    return fallback


def collect_sources(root: Path, targets: set[Path]) -> list[dict]:
    """Attach mod policy and dependency ancestry without reporting unselected files in changed-path mode."""
    context = set(targets)
    pending = set()
    manifests: dict[Path, dict] = {}
    for target in targets:
        mod = owning_mod(target, root)
        if mod:
            pending.add(mod)
    visited = set()
    while pending:
        mod = pending.pop()
        if mod in visited:
            continue
        visited.add(mod)
        manifest = read_manifest(mod)
        manifests[mod] = manifest
        context.update(source_files(mod))
        for dependency in manifest.get("depends", []):
            name = re.split(r"[<>=]", dependency, maxsplit=1)[0]
            for folder in ("mods", "mods_private"):
                candidate = root / folder / name
                if candidate.is_dir():
                    pending.add(candidate)
                    break
    widget = root / "mods/ui_base/src/Base/Lists/ModWidget.cs"
    if widget.is_file():
        context.add(widget)
    prefabs: dict[Path, bool] = {}
    result = []
    for path in sorted(context):
        relative = path.relative_to(root)
        mod = owning_mod(path, root)
        manifest = manifests.get(mod, {})
        policy = source_policy_manifest(path, mod, manifest) if mod else manifest
        categories = {str(value).lower() for value in policy.get("categories", [])}
        policy_id = str(policy.get("id", mod.name if mod else ""))
        fixture = "tests" in relative.parts or "test_suites" in relative.parts
        fixture = fixture or (mod is not None and mod.name.startswith("test_"))
        if mod is not None and mod not in prefabs:
            prefabs[mod] = production_prefab_exists(mod)
        result.append({
            "path": relative.as_posix(),
            "text": path.read_text(encoding="utf-8-sig"),
            "report": path in targets,
            "gameCode": relative.parts[0] in {"Core", "mods", "mods_private", "tests"},
            "checkGui": mod is not None and not fixture,
            "utilityGui": bool(categories & {"tools", "editor"}) or policy_id.endswith(("_editor", "_tool")),
            "hasScenePrefab": bool(mod and prefabs.get(mod)),
        })
    return result


def selected_paths(root: Path, values: list[str], paths_file: str | None) -> set[Path]:
    """Read caller-provided paths only; this tool never discovers changes through version control."""
    selections = list(values)
    if paths_file:
        file = Path(paths_file)
        text = sys.stdin.read() if paths_file == "-" else file.read_text(encoding="utf-8-sig")
        if text.lstrip().startswith("["):
            entries = json.loads(text)
            if not isinstance(entries, list) or any(not isinstance(value, str) for value in entries):
                raise ValueError("The changed-path JSON must be an array of strings.")
            selections.extend(entries)
        else:
            selections.extend(line.strip() for line in text.splitlines() if line.strip() and not line.lstrip().startswith("#"))
    targets = set()
    for value in selections:
        path = Path(value)
        path = (path if path.is_absolute() else root / path).resolve()
        if not path.is_relative_to(root):
            raise ValueError("A selected path is outside the project root: " + str(path))
        if not path.exists() and paths_file:
            continue
        if not path.exists():
            raise FileNotFoundError("Selected path does not exist: " + str(path))
        targets.update(source_files(path))
    return targets


def arguments() -> argparse.Namespace:
    """Require explicit paths, supporting both positional paths and caller-supplied changed-path lists."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*", help="Files or directories to check")
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--changed-paths", nargs="+", default=[], metavar="PATH")
    parser.add_argument("--paths-file", help="Newline-separated paths or a JSON array; no version-control lookup")
    parser.add_argument("--max-line-length", type=int, default=140)
    parser.add_argument("--json", type=Path, help="Write the complete machine-readable report")
    args = parser.parse_args()
    if not args.paths and not args.changed_paths and not args.paths_file:
        parser.error("Supply paths, --changed-paths, or --paths-file; implicit full-project scans are disabled.")
    return args


def main() -> int:
    """Exit 0 for clean selection, 1 for findings, 2 for invalid input or unavailable tooling."""
    args = arguments()
    try:
        root = args.root.resolve()
        targets = selected_paths(root, [*args.paths, *args.changed_paths], args.paths_file)
        inputs = collect_sources(root, targets)
        findings = analyze_sources(inputs, args.max_line_length) if targets else []
        report = {
            "schema": 1,
            "root": str(root),
            "checkedFiles": len(targets),
            "contextFiles": len(inputs) - len(targets),
            "maxLineLength": args.max_line_length,
            "issues": findings,
        }
        if args.json:
            args.json.parent.mkdir(parents=True, exist_ok=True)
            args.json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        for finding in findings:
            print(
                f"{finding['path']}:{finding['line']}:{finding['column']}: "
                f"{finding['rule']}: {finding['message']}"
            )
        print(f"Checked {len(targets)} C# file(s): {len(findings)} finding(s).")
        return 1 if findings else 0
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print("Style check failed: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
