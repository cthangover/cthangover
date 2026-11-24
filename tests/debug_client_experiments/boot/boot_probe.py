"""Measure the real DebugHost startup boundary in a disposable Godot project."""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time


HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
BROKEN = "test_mcp_boot_broken"
HEALTHY = "test_mcp_boot_healthy"
FAILURE = "BrokenBridge.cs(15,42): error CS0103:"
READY = "debug host ready:"
LATE = "warning after DebugHost startup"


def prepare(workspace, build_output):
    shutil.copytree(PROJECT / "Core", workspace / "Core")
    shutil.copytree(PROJECT / "config", workspace / "config")
    shutil.copytree(build_output, workspace / ".godot/mono/temp/bin/Debug")
    config_file = workspace / "config/mod_config.json"
    config = json.loads(config_file.read_text(encoding="utf-8-sig"))
    config["development_control"] = True
    config["development_discovery"] = "res://dev_host.json"
    config_file.write_text(json.dumps(config), encoding="utf-8")
    game_file = workspace / "config/game_config.json"
    game = json.loads(game_file.read_text(encoding="utf-8-sig"))
    game["logging"]["minimum_level"] = "Debug"
    game["logging"]["console_enabled"] = False
    game_file.write_text(json.dumps(game), encoding="utf-8")

    project = (PROJECT / "project.godot").read_text(encoding="utf-8-sig")
    project = re.sub(r"^run/main_scene=.*$", 'run/main_scene="res://probe.tscn"', project, flags=re.M)
    project = re.sub(r"^config/icon=.*\n", "", project, flags=re.M)
    (workspace / "project.godot").write_text(project, encoding="utf-8")
    (workspace / "probe.tscn").write_text(
        '[gd_scene format=3]\n\n[node name="Probe" type="Node"]\n', encoding="utf-8"
    )
    for mod in (BROKEN, HEALTHY):
        folder = workspace / "mods" / mod
        (folder / "src").mkdir(parents=True)
        manifest = {"id": mod, "name": mod, "version": "0.1.0", "sources": ["src/*.cs"]}
        (folder / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    shutil.copy2(HERE / "BrokenBridge.cs.txt", workspace / "mods" / BROKEN / "src/BrokenBridge.cs")
    for name in ("BootCommand.cs", "BootInitializer.cs"):
        shutil.copy2(HERE / (name + ".txt"), workspace / "mods" / HEALTHY / "src" / name)


def make_environment(root):
    environment = os.environ.copy()
    for name in ("APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "TMP", "TEMP", "TMPDIR"):
        folder = root / name.lower()
        folder.mkdir()
        environment[name] = str(folder)
    environment["CTHANGOVER_TEST_CACHE"] = str(root / "compilation-cache")
    return environment


def wait_for_discovery(process, discovery, timeout):
    until = time.monotonic() + timeout
    while time.monotonic() < until and process.poll() is None:
        try:
            descriptor = json.loads(discovery.read_text(encoding="utf-8"))
        except (FileNotFoundError, json.JSONDecodeError):
            time.sleep(0.05)
            continue
        assert descriptor["pid"] == process.pid, descriptor
        return descriptor
    raise RuntimeError("The isolated game did not publish its DebugHost endpoint.")


def request(process, discovery, environment, command, **fields):
    client = PROJECT / "DebugClient" / "dev_control.py"
    payload = json.dumps({"command": command, **fields})
    reply = subprocess.run(
        [sys.executable, "-B", "-X", "utf8", str(client), str(process.pid), payload,
         "--discovery", str(discovery)],
        env=environment, capture_output=True, encoding="utf-8", timeout=4,
    )
    if reply.returncode not in (0, 1):
        raise RuntimeError(f"{command}: {reply.stderr.strip()}")
    return json.loads(reply.stdout)


def probe_enabled(process, discovery, environment, timeout):
    endpoint = wait_for_discovery(process, discovery, timeout)
    status = request(process, discovery, environment, "status")
    assert status["ok"] and status["result"]["pid"] == process.pid, status
    assert endpoint["instance"] == status["result"]["instance"], status

    unavailable = request(process, discovery, environment, "compilation", provider=BROKEN)
    assert not unavailable["ok"], unavailable
    compilation = request(process, discovery, environment, "compilation", provider=HEALTHY)
    assert compilation["ok"], compilation
    assert any(BROKEN in entry and FAILURE in entry for entry in compilation["result"]), compilation
    tail = request(process, discovery, environment, "errors.pull")
    assert tail["ok"], tail
    entries = tail["result"]["entries"]
    assert any(LATE in entry["Message"] for entry in entries), tail
    assert any(entry.get("Mod") == BROKEN and entry.get("Code") == "CS0103"
               and entry.get("Line") == 15 for entry in entries), tail
    quit_reply = request(process, discovery, environment, "finish", provider=HEALTHY)
    assert quit_reply["ok"], quit_reply
    return {
        "failed_provider_error": unavailable["error"],
        "compiler_diagnostic": next(line.strip() for entry in compilation["result"]
                                    for line in entry.splitlines() if FAILURE in line),
        "startup_error_in_kernel": True,
        "startup_error_in_current_errors_pull": True,
        "late_warning_in_current_errors_pull": True,
    }


def godot_executable(command):
    executable = shutil.which(command)
    if executable is None:
        raise FileNotFoundError(f"Godot executable missing: {command}")
    if os.name == "nt":
        api = Path(executable).resolve().parent / "GodotSharp/Api/Debug"
        required = ("GodotPlugins.dll", "GodotPlugins.runtimeconfig.json", "GodotSharp.dll", "GodotSharpEditor.dll")
        missing = [name for name in required if not (api / name).is_file()]
        if missing:
            raise FileNotFoundError(f"Incomplete Windows Godot .NET installation at {api}: {', '.join(missing)}")
    return executable


def run(godot, build_output, mode, timeout):
    godot = godot_executable(godot)
    with tempfile.TemporaryDirectory(prefix="cthangover-mcp-boot-") as temp:
        root = Path(temp)
        workspace = root / "project"
        workspace.mkdir()
        prepare(workspace, build_output)
        environment = make_environment(root)
        native_log = root / "godot.log"
        core_log = root / "core.log"
        stdout = root / "godot.stdout"
        args = [godot, "--headless", "--path", str(workspace), "--log-file", str(native_log),
                "--", "--all-mods", f"--log-file={core_log}"]
        if mode == "disabled":
            args.append("--no-development-control")
        discovery = workspace / "dev_host.json"
        with stdout.open("wb") as output:
            process = subprocess.Popen(args, env=environment, stdout=output, stderr=subprocess.STDOUT)
            try:
                result = probe_enabled(process, discovery, environment, timeout) if mode == "enabled" else {}
                assert process.wait(timeout=timeout) == 0, process.returncode
                log = core_log.read_text(encoding="utf-8", errors="replace")
                engine_log = native_log.read_text(encoding="utf-8", errors="replace")
                assert "healthy fixture initialized" in log, log[-7000:]
                assert FAILURE in log, log[-7000:]
                assert "ObjectDB instances leaked" not in engine_log, engine_log[-7000:]
                assert "resources still in use" not in engine_log, engine_log[-7000:]
                if mode == "enabled":
                    assert log.index(FAILURE) < log.index(READY)
                else:
                    assert not discovery.exists() and READY not in log, log[-7000:]
                    result["startup_error_in_core_log"] = True
                    result["startup_error_in_stdout"] = FAILURE in stdout.read_text(encoding="utf-8", errors="replace")
                    result["debug_host_started"] = False
                return result
            except Exception:
                for path in (core_log, native_log, stdout):
                    if path.exists():
                        log = path.read_text(encoding="utf-8", errors="replace")
                        print(f"{path.name}\n{log[-2400:]}", file=sys.stderr)
                raise
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait(timeout=5)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN", "godot"))
    parser.add_argument("--build-output", type=Path, default=PROJECT / ".godot/mono/temp/bin/Debug")
    parser.add_argument("--mode", choices=("enabled", "disabled"), default="enabled")
    parser.add_argument("--timeout", type=int, default=16)
    options = parser.parse_args()
    print(json.dumps(run(options.godot, options.build_output, options.mode, options.timeout), indent=2))
