"""Exercise temporary settings, FPS sampling and transient scripts through the real MCP bridge."""

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import traceback
import time

from debug_client_smoke import HEALTHY, PROJECT, McpSession, godot_executable, make_environment, open_relay, prepare, wait_ready


SCRIPT = """
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods.Host;
using Cthangover.Core.Utils;

public sealed class RuntimeProbe : IDebugRuntimeScript
{
    public Task<JsonNode> RunAsync(DebugScriptContext context, CancellationToken cancellationToken)
    {
        context.Tree.Root.SetMeta("debug_runtime_probe", 42);
        context.MarkChanged();
        return Task.FromResult<JsonNode>(new JsonObject
        {
            ["main_thread"] = GameEnvironment.IsMainThread,
            ["value"] = context.Tree.Root.GetMeta("debug_runtime_probe").AsInt32()
        });
    }
}
"""

MOD_REFERENCE_SCRIPT = """
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods.Host;
using Cthangover.Mods.PerformanceOverlay;

public sealed class RuntimeModProbe : IDebugRuntimeScript
{
    public Task<JsonNode> RunAsync(DebugScriptContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult<JsonNode>(new JsonObject
        {
            ["monitor_exists"] = PerfOverlayNode.Current != null,
            ["previous_value"] = context.Tree.Root.GetMeta("debug_runtime_probe").AsInt32()
        });
    }
}
"""

TIMEOUT_SCRIPT = """
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods.Host;

public sealed class RuntimeTimeoutProbe : IDebugRuntimeScript
{
    public async Task<JsonNode> RunAsync(DebugScriptContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(2000, cancellationToken);
        return JsonValue.Create("timeout was ignored");
    }
}
"""


def command(session, name, *, provider=None, ok=True, **arguments):
    params = {"command": name, "arguments": arguments}
    if provider is not None:
        params["provider"] = provider
    answer = session.send("tools/call", {"name": "run", "arguments": params})
    data = answer["structuredContent"]
    assert data["ok"] is ok and bool(answer.get("isError")) is not ok, answer
    return data


def settings_and_perf(session):
    before = command(session, "settings.read")["result"]["effective"]
    applied = command(session, "settings.apply", preset="low_power")["result"]
    after = applied["effective"]
    assert applied["previous"] == before and not applied["persisted"], applied
    assert 0 < after["max_fps"] <= 30, after
    assert 0 < after["render_scale_3d"] <= 0.75001, after
    command(session, "settings.apply", values={"max_fps": -1}, ok=False)
    assert command(session, "settings.read")["result"]["effective"] == after

    initial = command(session, "perf.configure", provider="perf_overlay",
                      enabled=True, overlay=False, reset=True)["result"]
    assert initial["enabled"] and not initial["overlay"] and not initial["displayed"], initial
    deadline = time.monotonic() + 3
    while time.monotonic() < deadline:
        measured = command(session, "perf.read", provider="perf_overlay")["result"]
        if measured["frame"]["count"] >= 3:
            break
        time.sleep(0.05)
    assert measured["window_id"] == initial["window_id"] and measured["frame"]["count"] >= 3, measured
    assert measured["frame"]["mean_fps"] > 0 and measured["frame"]["p99_ms"] >= measured["frame"]["p95_ms"], measured
    assert measured["metrics"]["max_fps"] == after["max_fps"], measured
    stopped = command(session, "perf.configure", provider="perf_overlay", enabled=False)["result"]
    assert stopped["state"] == "stopped" and not stopped["enabled"], stopped
    time.sleep(0.3)
    frozen = command(session, "perf.read", provider="perf_overlay")["result"]
    assert frozen["frame"] == stopped["frame"], frozen

    restored = command(session, "settings.restore", restore_id=applied["restore_id"])["result"]
    assert restored["effective"] == before, restored
    return {"cap": after["max_fps"], "sample_count": measured["frame"]["count"], "restored": True}


def runtime_script(session, allowed):
    if not allowed:
        denied = command(session, "script.run", source=SCRIPT, ok=False)
        assert "--allow-debug-scripts" in json.dumps(denied), denied
        return {"script_denied_without_opt_in": True}

    result = command(session, "script.run", source=SCRIPT)["result"]
    assert result["value"] == {"main_thread": True, "value": 42}, result
    assert result["unload"] == "collected", result
    typed = command(session, "script.run", source=MOD_REFERENCE_SCRIPT, mods=["perf_overlay"])["result"]
    assert typed["value"] == {"monitor_exists": True, "previous_value": 42} and typed["unload"] == "collected", typed
    failed = command(session, "script.run", source=SCRIPT.replace("42", "MissingRuntimeSymbol"), ok=False)["result"]
    assert failed["unload"] == "not_loaded", failed
    diagnostic = next(item for item in failed["diagnostics"] if item["code"] == "CS0103")
    assert diagnostic["line"] > 0 and diagnostic["column"] > 0, diagnostic
    timeout = command(session, "script.run", source=TIMEOUT_SCRIPT, timeout_ms=300, ok=False)["result"]
    assert timeout["stage"] == "timeout" and timeout["unload"] == "collected", timeout
    return {"script_on_game_thread": True, "script_unload": result["unload"], "script_timeout": True,
            "typed_reference": True, "compiler_error": diagnostic["code"]}


def exercise_game(godot, workspace, root, environment, allow_scripts, *, verify_warm_cache=False):
    label = "script-on" if allow_scripts else "script-off"
    core_log = root / f"core-{label}.log"
    native_log = root / f"godot-{label}.log"
    stdout = root / f"stdout-{label}.log"
    args = [godot, "--headless", "--path", str(workspace), "--log-file", str(native_log), "--", "--all-mods",
            "--with-mods=debug_client,perf_overlay", "--no-hot-reload", f"--log-file={core_log}"]
    if allow_scripts:
        args.append("--allow-debug-scripts")
    with stdout.open("wb") as output, subprocess.Popen(args, env=environment, stdout=output,
                                                       stderr=subprocess.STDOUT) as game:
        try:
            wait_ready(game, core_log, "Boot (sync) complete")
            relay_args = [sys.executable, "-B", "-X", "utf8", str(PROJECT / "DebugClient/mcp_stdio.py"),
                          "--pid", str(game.pid)]
            with open_relay(relay_args, environment) as relay:
                session = McpSession(relay)
                session.send("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                             "clientInfo": {"name": "cthangover-debug-features", "version": "1"}})
                session.send("notifications/initialized", notify=True)
                result = settings_and_perf(session) if allow_scripts else {}
                result.update(runtime_script(session, allow_scripts))
                if verify_warm_cache:
                    expected = {"debug_client": "hit", "ui_base": "hit", "perf_overlay": "hit", HEALTHY: "compiled"}
                    actual = {}
                    for mod in expected:
                        details = session.tool("inspect", area="cache", mod=mod)
                        actual[mod] = details.get("outcome")
                    assert actual == expected, actual
                    result["warm_cache"] = actual
                command(session, "finish", provider=HEALTHY)
                assert game.wait(timeout=8) == 0
            log = native_log.read_text(encoding="utf-8", errors="replace")
            assert "ObjectDB instances leaked" not in log and "resources still in use" not in log
            return result
        except Exception:
            traceback.print_exc()
            for log in (core_log, native_log, stdout):
                if log.exists():
                    print(f"{log.name}\n{log.read_text(encoding='utf-8', errors='replace')[-1200:]}", file=sys.stderr)
            raise
        finally:
            if game.poll() is None:
                game.kill()
                game.wait(timeout=5)


def run(godot, build_output, allow_scripts, restart_cache):
    godot = godot_executable(godot)
    with tempfile.TemporaryDirectory(prefix="cthangover-debug-features-") as directory:
        root = Path(directory)
        workspace = root / "project"
        workspace.mkdir()
        prepare(workspace, build_output)
        for mod in ("debug_client", "ui_base", "perf_overlay"):
            shutil.copytree(PROJECT / "mods" / mod, workspace / "mods" / mod,
                            ignore=shutil.ignore_patterns(".git", "source_code", "docs", "test_suites", "bin", "obj"))
        watchdog = workspace / "mods" / HEALTHY / "src/BootInitializer.cs"
        watchdog.write_text(watchdog.read_text(encoding="utf-8").replace("6.0", "20.0"), encoding="utf-8")
        config = workspace / "config/mod_config.json"
        values = json.loads(config.read_text(encoding="utf-8"))
        values.pop("development_discovery", None)
        values["development_control"] = False
        config.write_text(json.dumps(values), encoding="utf-8")

        environment = make_environment(root)
        result = exercise_game(godot, workspace, root, environment, allow_scripts)
        if restart_cache:
            fixture = workspace / "mods" / HEALTHY / "src/BootCommand.cs"
            with fixture.open("a", encoding="utf-8") as source:
                source.write("\n// Changed between two launches with the same core and cache.\n")
            result.update(exercise_game(godot, workspace, root, environment, False, verify_warm_cache=True))
        return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default="godot")
    parser.add_argument("--build-output", type=Path, default=PROJECT / ".godot/mono/temp/bin/Debug")
    parser.add_argument("--without-script-opt-in", action="store_true")
    parser.add_argument("--restart-cache", action="store_true",
                        help="Relaunch using the same cache after editing a single healthy fixture mod.")
    options = parser.parse_args()
    if options.restart_cache and options.without_script_opt_in:
        parser.error("--restart-cache performs the initial launch with script opt-in.")
    print(json.dumps(run(options.godot, options.build_output, not options.without_script_opt_in,
                         options.restart_cache), indent=2))
