"""Run the shipped debug_client mod and its stdio relay in an isolated Godot project."""

import argparse
from contextlib import contextmanager
import json
from pathlib import Path
import queue
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import uuid


EXPERIMENTS = Path(__file__).resolve().parent / "debug_client_experiments"
sys.path.insert(0, str(EXPERIMENTS / "boot"))
from boot_probe import BROKEN, HEALTHY, PROJECT, godot_executable, make_environment, prepare, request


class McpSession:
    """Sends standard textual MCP requests through the shipped relay with bounded reads."""

    def __init__(self, process):
        self.process = process
        self.replies = queue.Queue()
        self.request_id = 0
        threading.Thread(target=self.read_output, daemon=True).start()

    def read_output(self):
        for line in self.process.stdout:
            self.replies.put(line)
        self.replies.put(None)

    def send(self, method, params=None, notify=False, wait=True):
        message = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            message["params"] = params
        if not notify:
            self.request_id += 1
            message["id"] = self.request_id
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()
        if notify or not wait:
            return None
        answer = json.loads(self.replies.get(timeout=5))
        assert answer["id"] == self.request_id and "result" in answer, answer
        return answer["result"]

    def tool(self, name, **arguments):
        response = self.send("tools/call", {"name": name, "arguments": arguments})
        assert not response.get("isError"), response
        data = response["structuredContent"]
        assert data["ok"], response
        return data["result"]


def install_fixtures(workspace):
    mod = PROJECT / "mods" / "debug_client"
    assert (mod / "lib/DebugClient.Mcp.dll").is_file(), "Build the Release MCP bridge first."
    shutil.copytree(mod, workspace / "mods/debug_client",
                    ignore=shutil.ignore_patterns(".git", "source_code", "docs"))
    shutil.copyfile(PROJECT / "tests/fixtures/debug_client/UnloadDuringCommand.cs.txt",
                    workspace / "mods" / HEALTHY / "src/UnloadDuringCommand.cs")


def wait_ready(game, core_log, marker):
    deadline = time.monotonic() + 10
    while game.poll() is None and time.monotonic() < deadline:
        if core_log.exists() and marker in core_log.read_text(encoding="utf-8", errors="replace"):
            return
        time.sleep(0.05)
    raise RuntimeError(f"The game did not reach {marker}")


def exercise(game, relay, environment, discovery):
    session = McpSession(relay)
    hello = session.send("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                        "clientInfo": {"name": "cthangover-smoke", "version": "1"}})
    assert hello["serverInfo"]["name"], hello
    session.send("notifications/initialized", notify=True)
    tools = session.send("tools/list", {})
    assert {tool["name"] for tool in tools["tools"]} == {"diagnose", "inspect", "describe", "run"}, tools

    diagnosis = session.tool("diagnose", mod=BROKEN, since=0)
    compiler_error = next(entry for entry in diagnosis["entries"] if entry.get("Code") == "CS0103")
    assert compiler_error["File"].endswith("BrokenBridge.cs") and compiler_error["Line"] == 15, diagnosis
    assert diagnosis["cache"]["outcome"] == "compile_failed" and not diagnosis["mod"]["has_loaded_assembly"], diagnosis
    nodes = session.tool("inspect", area="node", query="SceneManager")
    assert any(node["path"] == "/root/SceneManager" for node in nodes["entries"]), nodes
    described = session.tool("describe", mod=HEALTHY, query="compilation")
    assert any(entry["Command"] == "compilation" and entry["Owner"] == HEALTHY for entry in described["entries"]), described

    operation_id = str(uuid.uuid4())
    result = session.tool("run", command="compilation", provider=HEALTHY, operation_id=operation_id)
    assert any("BrokenBridge.cs" in entry for entry in result), result
    receipt = session.tool("run", command="operation.status", operation_id=operation_id)
    assert receipt["state"] == "completed" and receipt["response"]["result"] == result, receipt

    expected_host = request(game, discovery, environment, "status")["result"]["instance"]
    pending_id = str(uuid.uuid4())
    session.send("tools/call", {"name": "run", "arguments": {
        "command": "test.debug.unload_during_call", "provider": HEALTHY, "operation_id": pending_id}}, wait=False)
    assert session.replies.get(timeout=5) is None
    assert relay.wait(timeout=5) == 0, relay.stderr.read()
    deadline = time.monotonic() + 3
    while True:
        receipt = request(game, discovery, environment, "operation.status", operation_id=pending_id)
        assert receipt["ok"], receipt
        if receipt["result"]["state"] not in {"queued", "running"} or time.monotonic() >= deadline:
            break
        time.sleep(0.04)
    assert receipt["result"]["state"] == "cancelled", receipt
    deadline = time.monotonic() + 3
    while True:
        unload = request(game, discovery, environment, "test.debug.unload_result", provider=HEALTHY)
        if unload.get("result") is not None or time.monotonic() >= deadline:
            break
        time.sleep(0.04)
    assert unload["ok"] and unload["result"] == {"released": ["debug_client"], "alive": []}, unload
    assert request(game, discovery, environment, "status")["result"]["instance"] == expected_host
    assert not discovery.exists(), "The default transport unexpectedly wrote a discovery file."
    assert request(game, discovery, environment, "finish", provider=HEALTHY)["ok"]
    return {"mcp_tools": sorted(tool["name"] for tool in tools["tools"]), "compiler_error": compiler_error["Code"],
            "operation_receipt": "completed", "pending_receipt": "cancelled", "mcp_closed_on_unload": True,
            "same_kernel_host": True}


@contextmanager
def open_relay(args, environment):
    with subprocess.Popen(args, env=environment, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, encoding="utf-8") as relay:
        try:
            yield relay
        finally:
            if relay.poll() is None:
                relay.kill()
                relay.wait(timeout=5)


def run(godot, build_output, selected):
    godot = godot_executable(godot)
    with tempfile.TemporaryDirectory(prefix="cthangover-debug-smoke-") as directory:
        root = Path(directory)
        workspace = root / "project"
        workspace.mkdir()
        prepare(workspace, build_output)
        install_fixtures(workspace)
        config = workspace / "config/mod_config.json"
        settings = json.loads(config.read_text(encoding="utf-8"))
        settings.pop("development_discovery", None)
        settings["development_control"] = selected
        config.write_text(json.dumps(settings), encoding="utf-8")
        environment = make_environment(root)
        core_log = root / "core.log"
        native_log = root / "godot.log"
        stdout = root / "stdout.log"
        discovery = workspace / "dev_host.json"
        args = [godot, "--headless", "--path", str(workspace), "--log-file", str(native_log),
                "--", "--all-mods", "--no-hot-reload", f"--log-file={core_log}"]
        if selected:
            args.append("--with-mods=debug_client")
        with stdout.open("wb") as output, subprocess.Popen(args, env=environment, stdout=output,
                                                           stderr=subprocess.STDOUT) as game:
            try:
                relay_args = [sys.executable, "-B", "-X", "utf8", str(PROJECT / "DebugClient/mcp_stdio.py"),
                              "--pid", str(game.pid)]
                if selected:
                    wait_ready(game, core_log, "debug host ready:")
                    with open_relay(relay_args, environment) as relay:
                        session = McpSession(relay)
                        session.send("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                     "clientInfo": {"name": "reconnect-probe", "version": "1"}})
                        relay.stdin.close()
                        assert relay.wait(timeout=5) == 0, relay.stderr.read()
                    with open_relay(relay_args, environment) as relay:
                        result = exercise(game, relay, environment, discovery)
                        result["reconnected"] = True
                else:
                    wait_ready(game, core_log, "Boot (sync) complete")
                    relay = subprocess.run(relay_args + ["--connect-timeout", "0.2"], env=environment,
                                           capture_output=True, encoding="utf-8", timeout=3)
                    assert relay.returncode == 2 and not relay.stdout, relay
                    assert game.poll() is None
                    log = core_log.read_text(encoding="utf-8", errors="replace")
                    assert "MCP listening on" not in log and "debug host ready:" not in log and not discovery.exists()
                    result = {"implicit_all_mods_launch_mcp_off": True, "legacy_pipe_off": True}
                assert game.wait(timeout=8) == 0
                log = native_log.read_text(encoding="utf-8", errors="replace")
                assert "ObjectDB instances leaked" not in log and "resources still in use" not in log
                return result
            except Exception:
                for log in (core_log, native_log, stdout):
                    if log.exists():
                        print(log.read_text(encoding="utf-8", errors="replace")[-4200:], file=sys.stderr)
                raise
            finally:
                if game.poll() is None:
                    game.kill()
                    game.wait(timeout=5)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default="godot")
    parser.add_argument("--build-output", type=Path, default=PROJECT / ".godot/mono/temp/bin/Debug")
    parser.add_argument("--unselected", action="store_true", help="Verify that --all-mods alone never opens the bridge.")
    options = parser.parse_args()
    print(json.dumps(run(options.godot, options.build_output, not options.unselected), indent=2))
