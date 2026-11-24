"""Build and boot an isolated core several times to verify the mod cache contract."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import traceback

from debug_client_smoke import HEALTHY, PROJECT, godot_executable, make_environment, prepare, request, wait_ready


MOD = "test_core_cache_contract"
CONTRACT_PATH = Path("mod_reference_assemblies/core")
CORE_SOURCE = """
namespace Cthangover.Core
{
    /// <summary>Provides an API boundary whose implementation and signature the smoke test can vary independently.</summary>
    public static class CacheAbiProbe
    {
        /// <summary>Lets the launched game identify which kernel implementation it is executing.</summary>
        public static TYPE Marker() => VALUE;
    }
}
"""
MOD_SOURCE = """
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core;
using Cthangover.Core.Mods.HotReload;

/// <summary>Calls the current kernel API from an assembly that can be reused between launches.</summary>
public sealed class CacheContractCommand : IModDevelopmentCommand
{
    /// <summary>Lets the smoke test call through the actual runtime mod assembly.</summary>
    public IReadOnlyCollection<string> Commands { get; } = new[] { "cache_contract.probe" };

    /// <summary>Reports the core marker obtained over the compiled API boundary.</summary>
    public string Description => "Read the core marker from the cached mod.";

    /// <summary>Returns the marker as detached JSON so the caller can check the active implementation.</summary>
    public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
    {
        return Task.FromResult<JsonNode>(JsonValue.Create(CacheAbiProbe.Marker()));
    }
}
"""


def stage_build_sources(source):
    source.mkdir()
    shutil.copytree(PROJECT / "Core", source / "Core")
    for name in ("Cthangover.csproj", "Directory.Build.props", "Directory.Build.targets", "global.json", "project.godot"):
        shutil.copyfile(PROJECT / name, source / name)


def build(source, marker_type, marker, *, restore=False):
    probe = source / "Core/CacheAbiProbe.cs"
    body = CORE_SOURCE.replace("TYPE", marker_type).replace("VALUE", str(marker))
    if not probe.exists() or probe.read_text(encoding="utf-8") != body:
        probe.write_text(body, encoding="utf-8")
    args = ["dotnet", "build", "Cthangover.csproj", "--nologo", "--verbosity", "quiet"]
    if not restore:
        args.append("--no-restore")
    subprocess.run(args, cwd=source, check=True, timeout=45)
    return source / ".godot/mono/temp/bin/Debug"


def read_contract(output):
    package = output / CONTRACT_PATH
    hashes = (package / "contract.sha256").read_text(encoding="utf-8-sig").splitlines()
    assert len(hashes) == 2, hashes
    actual = hashlib.sha256((output / "Cthangover.dll").read_bytes()).hexdigest()
    reference = hashlib.sha256((package / "Cthangover.dll").read_bytes()).hexdigest()
    assert [value.lower() for value in hashes] == [actual, reference], hashes
    return actual, reference


def install_mod(workspace):
    mod = workspace / "mods" / MOD
    (mod / "src").mkdir(parents=True)
    metadata = {"id": MOD, "name": MOD, "version": "0.1.0", "sources": ["src/*.cs"]}
    (mod / "manifest.json").write_text(json.dumps(metadata), encoding="utf-8")
    (mod / "src/CacheContractCommand.cs").write_text(MOD_SOURCE, encoding="utf-8")


def launch(godot, workspace, environment, root, label):
    core_log = root / f"core-{label}.log"
    native_log = root / f"godot-{label}.log"
    discovery = workspace / "dev_host.json"
    discovery.unlink(missing_ok=True)
    args = [godot, "--headless", "--path", str(workspace), "--log-file", str(native_log), "--", "--all-mods",
            "--no-hot-reload", f"--log-file={core_log}"]
    with subprocess.Popen(args, env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) as game:
        try:
            wait_ready(game, core_log, "debug host ready:")
            cache = request(game, discovery, environment, "inspect", area="cache", mod=MOD)
            output = request(game, discovery, environment, "cache_contract.probe", provider=MOD)
            assert cache["ok"] and output["ok"], (cache, output)
            assert request(game, discovery, environment, "finish", provider=HEALTHY)["ok"]
            assert game.wait(timeout=6) == 0
            log = native_log.read_text(encoding="utf-8", errors="replace")
            assert "ObjectDB instances leaked" not in log and "resources still in use" not in log
            return {"value": output["result"], "outcome": cache["result"]["outcome"],
                    "artifact": Path(cache["result"]["compiled_artifact"]).name}
        except Exception:
            traceback.print_exc()
            for log in (core_log, native_log):
                if log.exists():
                    print(log.read_text(encoding="utf-8", errors="replace")[-1200:], file=sys.stderr)
            raise
        finally:
            if game.poll() is None:
                game.kill()
                game.wait(timeout=5)


def run(godot):
    godot = godot_executable(godot)
    with tempfile.TemporaryDirectory(prefix="cthangover-core-contract-") as directory:
        root = Path(directory)
        source = root / "build"
        workspace = root / "project"
        stage_build_sources(source)
        initial_output = build(source, "int", 101, restore=True)
        initial_impl, initial_api = read_contract(initial_output)
        stale = root / "old-contract"
        shutil.copytree(initial_output / CONTRACT_PATH, stale)
        initial_dll = initial_output / "Cthangover.dll"
        timestamp = initial_dll.stat().st_mtime_ns
        build(source, "int", 101)
        assert initial_dll.stat().st_mtime_ns == timestamp, "An unchanged core was rewritten."

        workspace.mkdir()
        prepare(workspace, initial_output)
        install_mod(workspace)
        environment = make_environment(root)
        cold = launch(godot, workspace, environment, root, "cold")
        assert cold["value"] == 101 and cold["outcome"] == "compiled", cold

        output = build(source, "int", 202)
        impl, api = read_contract(output)
        assert impl != initial_impl and api == initial_api, (impl, api)
        runtime = workspace / ".godot/mono/temp/bin/Debug"
        shutil.copytree(output, runtime, dirs_exist_ok=True)
        warm = launch(godot, workspace, environment, root, "warm")
        assert warm == {**cold, "value": 202, "outcome": "hit"}, (cold, warm)

        output = build(source, "long", 303)
        _, api = read_contract(output)
        assert api != initial_api, api
        shutil.copytree(output, runtime, dirs_exist_ok=True)
        abi_change = launch(godot, workspace, environment, root, "abi-change")
        assert abi_change["value"] == 303 and abi_change["outcome"] == "compiled", abi_change
        assert abi_change["artifact"] != cold["artifact"], abi_change

        shutil.copytree(stale, runtime / CONTRACT_PATH, dirs_exist_ok=True)
        fallback = launch(godot, workspace, environment, root, "stale-contract")
        assert fallback["value"] == 303 and fallback["outcome"] == "compiled", fallback
        assert fallback["artifact"] not in {cold["artifact"], abi_change["artifact"]}, fallback
        return {"unchanged_build_reused_output": True, "cold": cold, "body_changed": warm,
                "public_signature_changed": abi_change, "stale_contract_fallback": fallback}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default="godot")
    options = parser.parse_args()
    print(json.dumps(run(options.godot), indent=2))
