"""Verify MCP diagnostics and bridge unload in a disposable Linux Godot game."""

import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile


HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "boot"))
from boot_probe import BROKEN, FAILURE, HEALTHY, PROJECT, make_environment, prepare, request, wait_for_discovery


BRIDGE = "test_mcp_live_bridge"
DIRECT = "test_mcp_live_sdk_source"
DIRECT_FAILURE = "error CS0619: 'Implementation.Implementation()'"


def add_bridge(root, workspace):
    dependencies = root / "packages"
    dependencies.mkdir()
    package_project = dependencies / "McpProbeService.csproj"
    shutil.copyfile(HERE / package_project.name, package_project)
    shutil.copyfile(HERE / "McpProbeService.cs.txt", dependencies / "McpProbeService.cs.txt")
    subprocess.run(["dotnet", "build", str(package_project), "--verbosity", "quiet", "-p:NuGetAudit=false"],
                   check=True, timeout=12)
    folder = workspace / "mods" / BRIDGE
    (folder / "src").mkdir(parents=True)
    (folder / "lib").mkdir()
    manifest = {"id": BRIDGE, "name": BRIDGE, "version": "0.1.0", "sources": ["src/*.cs"],
                "references": ["lib/*.dll"]}
    (folder / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    for dll in (dependencies / "bin" / "Debug" / "net10.0").glob("*.dll"):
        shutil.copyfile(dll, folder / "lib" / dll.name)
    shutil.copyfile(HERE / "LiveMcpBridge.cs.txt", folder / "src" / "LiveMcpBridge.cs")
    shutil.copyfile(HERE / "UnloadBridgeCommand.cs.txt", workspace / "mods" / HEALTHY / "src" / "UnloadBridgeCommand.cs")
    direct = workspace / "mods" / DIRECT
    (direct / "src").mkdir(parents=True)
    manifest = {"id": DIRECT, "name": DIRECT, "version": "0.1.0", "sources": ["src/*.cs"], "depends": [BRIDGE]}
    (direct / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    shutil.copyfile(HERE / "DirectSdkTypes.cs.txt", direct / "src" / "DirectSdkTypes.cs")


def rpc(stream, method, params=None, request_id=None):
    message = {"jsonrpc": "2.0", "method": method}
    if params is not None:
        message["params"] = params
    if request_id is not None:
        message["id"] = request_id
    stream.write(json.dumps(message).encode("utf-8") + b"\n")
    stream.flush()
    if request_id is None:
        return None
    reply = json.loads(stream.readline())
    assert reply["id"] == request_id and "result" in reply, reply
    return reply["result"]


def exercise(process, discovery, environment, timeout):
    endpoint = wait_for_discovery(process, discovery, timeout)
    address = str(Path(environment["TMPDIR"]) / f"CoreFxPipe_ct-mcp-live-{process.pid}")
    with socket.socket(socket.AF_UNIX) as client:
        client.settimeout(timeout)
        client.connect(address)
        with client.makefile("rwb") as stream:
            hello = rpc(stream, "initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                        "clientInfo": {"name": "live-test", "version": "1"}}, 1)
            assert hello["serverInfo"]["name"] == "live_mcp_probe", hello
            rpc(stream, "notifications/initialized")
            listed = rpc(stream, "tools/list", {}, 2)
            assert listed["tools"][0]["inputSchema"]["required"] == ["mod"], listed
            result = rpc(stream, "tools/call", {"name": "diagnose", "arguments": {"mod": BROKEN}}, 3)
            diagnostic = result["content"][0]["text"]
            assert FAILURE in diagnostic, result
            result = rpc(stream, "tools/call", {"name": "diagnose", "arguments": {"mod": DIRECT}}, 4)
            sdk_diagnostic = result["content"][0]["text"]
            assert DIRECT_FAILURE in sdk_diagnostic, result
            unload = request(process, discovery, environment, "unload_mcp_fixture", provider=HEALTHY)
            assert unload["ok"] and unload["result"]["released"] == [BRIDGE], unload
            assert stream.readline() == b"", "Existing MCP connection did not close."
    with socket.socket(socket.AF_UNIX) as client:
        try:
            client.connect(address)
        except (FileNotFoundError, ConnectionRefusedError):
            pass
        else:
            raise AssertionError("MCP accepted a new connection after unload.")
    status = request(process, discovery, environment, "status")
    assert status["ok"] and status["result"]["instance"] == endpoint["instance"], status
    finish = request(process, discovery, environment, "finish", provider=HEALTHY)
    assert finish["ok"], finish
    return {"diagnostic": next(line.strip() for line in diagnostic.splitlines() if FAILURE in line),
            "direct_sdk_diagnostic": next(line.strip() for line in sdk_diagnostic.splitlines() if DIRECT_FAILURE in line),
            "bridge_session_closed": True, "new_connection_rejected": True,
            "same_kernel_host_after_unload": True, "context_survivors": unload["result"]["alive"]}


def run(godot, build_output, timeout):
    with tempfile.TemporaryDirectory(prefix="ct-mcp-live-") as temp:
        root = Path(temp)
        workspace = root / "project"
        workspace.mkdir()
        prepare(workspace, build_output)
        add_bridge(root, workspace)
        environment = make_environment(root)
        native_log = root / "godot.log"
        core_log = root / "core.log"
        args = [godot, "--headless", "--path", str(workspace), "--log-file", str(native_log),
                "--", "--all-mods", f"--log-file={core_log}"]
        process = subprocess.Popen(args, env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            result = exercise(process, workspace / "dev_host.json", environment, timeout)
            assert process.wait(timeout=timeout) == 0
            assert not result["context_survivors"], result
            game_log = native_log.read_text(encoding="utf-8", errors="replace")
            assert "ObjectDB instances leaked" not in game_log and "resources still in use" not in game_log
            print(json.dumps(result, indent=2))
        except Exception:
            for logfile in (core_log, native_log):
                if logfile.exists():
                    print(logfile.read_text(encoding="utf-8", errors="replace")[-5000:], file=sys.stderr)
            raise
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)


if __name__ == "__main__":
    if sys.platform != "linux":
        raise SystemExit("This probe uses the Linux .NET named pipe socket.")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN", "godot"))
    parser.add_argument("--build-output", type=Path, default=PROJECT / ".godot/mono/temp/bin/Debug")
    parser.add_argument("--timeout", type=float, default=5)
    options = parser.parse_args()
    run(options.godot, options.build_output, options.timeout)
