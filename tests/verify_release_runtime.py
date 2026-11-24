#!/usr/bin/env python3
"""Verify the exported runtime, PCK content and loose mod set before archiving.

Проверка экспортированного CLR по runtime pack перед упаковкой релиза.
Copied/unpacked verification payloads must match the current export.
SDK 10/net10.0 compile the game; the self-contained payload includes CLR 10
and the SDK reference pack needed to compile mods without an installed SDK.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import sys
import xml.etree.ElementTree as ET


def pck_paths(path):
    """Read the unencrypted standalone Godot 4 PCK directory (formats 2/3).

    Layout: godot/4.6/core/io/file_access_pack.cpp, PackedSourcePCK::try_open_pack.
    """
    with path.open("rb") as stream:
        header = stream.read(40)
        magic, version, _major, _minor, _patch, flags, _base, directory = struct.unpack("<6I2Q", header)
        if magic != 0x43504447 or version not in (2, 3) or flags & 1:
            raise ValueError("release content audit requires an unencrypted Godot 4 PCK")
        stream.seek(directory if version == 3 else 96)
        count = struct.unpack("<I", stream.read(4))[0]
        paths = []
        for _ in range(count):
            length = struct.unpack("<I", stream.read(4))[0]
            path = stream.read(length).rstrip(b"\0").decode("utf-8").removeprefix("res://")
            stream.read(36)  # offset, size, MD5 and file flags
            paths.append(path)
        return paths


def verify_content(root):
    """Reject development trees in the PCK and fixture mods in loose content."""
    packs = list(root.glob("*.pck"))
    if len(packs) != 1:
        raise ValueError(f"expected one standalone game PCK, found {len(packs)}")
    paths = pck_paths(packs[0])
    forbidden = {"jobs", "site_github", "comfy", "node_modules", "authoring3d", "docs", "tests", "sdk", "DebugClient",
                 "site", "mockups", "gen", "undefined", "releases", "mods_private", "collections", "config"}
    unwanted = []
    for path in paths:
        parts = path.split("/")
        if (parts[0] in forbidden or any(part in {"bin", "obj", "__pycache__"} for part in parts)
                or path in {"global.json", "dev_host.json", "dev_host_probe.json"} or path.startswith("scenes/test/")):
            unwanted.append(path)
        elif parts[0] == "mods" and (parts[1].startswith("test_") or "test_suites" in parts or "source_code" in parts):
            unwanted.append(path)
    if unwanted:
        raise ValueError(f"development content leaked into PCK: {unwanted[:5]}")
    source = Path(__file__).resolve().parents[1] / "mods"
    expected = {path.parent.name for path in source.glob("*/manifest.json")
                if not json.loads(path.read_text(encoding="utf-8-sig")).get("fixture", False)}
    actual = {path.parent.name for path in (root / "mods").glob("*/manifest.json")}
    if actual != expected:
        raise ValueError(f"release mod set differs: missing={sorted(expected - actual)}, extra={sorted(actual - expected)}")
    libraries = ("DebugClient.Mcp.dll", "ModelContextProtocol.Core.dll", "Microsoft.Extensions.AI.Abstractions.dll",
                 "Microsoft.Extensions.DependencyInjection.Abstractions.dll", "Microsoft.Extensions.Logging.Abstractions.dll")
    for path in ("DebugClient/mcp_stdio.py", *(f"mods/debug_client/lib/{library}" for library in libraries)):
        if not (root / path).is_file():
            raise ValueError(f"release debug client is missing {path}")
    print(f"release content: {len(paths)} PCK files; {len(actual)} runtime mods; no development trees or fixtures")


def verify(root, rid, packages, expected_release=None):
    project = ET.parse(Path(__file__).resolve().parents[1] / "Cthangover.csproj")
    version = project.findtext(".//ReleaseRuntimeVersion")
    target = project.findtext(".//TargetFramework")
    if target != "net10.0" or not version or not version.startswith("10."):
        raise ValueError("the release contract requires net10.0 and a CLR 10 servicing version")
    configs = list(root.rglob("Cthangover.runtimeconfig.json"))
    if len(configs) != 1:
        raise ValueError(f"expected one game runtimeconfig, found {len(configs)}")
    folder = configs[0].parent
    core = folder / "Cthangover.dll"
    core_reference = Path("mod_reference_assemblies/core/Cthangover.dll")
    core_receipt = Path("mod_reference_assemblies/core/contract.sha256")
    core_hash = hashlib.sha256(core.read_bytes()).hexdigest()
    reference_hash = hashlib.sha256((folder / core_reference).read_bytes()).hexdigest()
    receipt_hashes = (folder / core_receipt).read_text(encoding="utf-8-sig").lower().splitlines()
    if core_hash == reference_hash or receipt_hashes != [core_hash, reference_hash]:
        raise ValueError("exported core mod contract does not match its assemblies")
    if expected_release is not None:
        built = list(expected_release.rglob("Cthangover.runtimeconfig.json"))
        if len(built) != 1:
            raise ValueError("cannot identify the current build's managed payload")
        for name in (core.name, "Cthangover.deps.json", "Cthangover.runtimeconfig.json", core_reference, core_receipt):
            if (folder / name).read_bytes() != (built[0].parent / name).read_bytes():
                raise ValueError(f"verify payload {name} differs from the release just built")
    print(f"release core: sha256={core_hash}; reference sha256={reference_hash}")
    if not (folder / "Godot.SourceGenerators.dll").is_file():
        raise ValueError("runtime mod compiler requires exported Godot.SourceGenerators.dll")
    config = json.loads(configs[0].read_text(encoding="utf-8"))["runtimeOptions"]
    frameworks = config.get("includedFrameworks", [])
    if config.get("tfm") != target or not any(f.get("name") == "Microsoft.NETCore.App" and f.get("version") == version for f in frameworks):
        raise ValueError(f"self-contained {target} payload must include Microsoft.NETCore.App {version}")
    deps = json.loads((folder / "Cthangover.deps.json").read_text(encoding="utf-8"))
    pack_name = f"Microsoft.NETCore.App.Runtime.{rid}"
    if f"runtimepack.{pack_name}/{version}" not in deps["libraries"]:
        raise ValueError(f"game deps do not resolve {pack_name} {version}")
    pack = packages / pack_name.lower() / version
    names = ["System.Private.CoreLib.dll"]
    names += ["coreclr.dll", "clrjit.dll", "hostfxr.dll", "hostpolicy.dll"] if rid.startswith("win-") else ["libcoreclr.so", "libclrjit.so", "libhostfxr.so", "libhostpolicy.so"]
    for name in names:
        originals = list(pack.rglob(name))
        actual = folder / name
        if len(originals) != 1 or not actual.is_file():
            raise ValueError(f"missing/ambiguous runtime pack or exported file: {name} ({pack})")
        if hashlib.sha256(actual.read_bytes()).digest() != hashlib.sha256(originals[0].read_bytes()).digest():
            raise ValueError(f"exported {name} differs from runtime pack {version}")
    references = folder / "mod_reference_assemblies" / target
    reference_version = (references / "pack.version").read_text(encoding="utf-8").strip()
    if not reference_version.startswith("10."):
        raise ValueError("runtime mod compilation requires the net10.0 reference pack")
    dotnet = shutil.which("dotnet")
    dotnet_root = Path(os.environ.get("DOTNET_ROOT") or (Path(dotnet).resolve().parent if dotnet else ""))
    candidates = [dotnet_root / "packs" / "Microsoft.NETCore.App.Ref" / reference_version / "ref" / target,
                  packages / "microsoft.netcore.app.ref" / reference_version / "ref" / target]
    reference_pack = next((path for path in candidates if (path / "System.Runtime.dll").is_file()), None)
    if reference_pack is None:
        raise ValueError(f"cannot verify exported reference pack {reference_version}: {candidates}")
    originals = {path.name: path for path in reference_pack.glob("*.dll")}
    actuals = {path.name: path for path in references.glob("*.dll")}
    if not originals or originals.keys() != actuals.keys():
        raise ValueError("exported net10.0 reference assembly set is incomplete or contains extra files")
    for name, original in originals.items():
        if hashlib.sha256(actuals[name].read_bytes()).digest() != hashlib.sha256(original.read_bytes()).digest():
            raise ValueError(f"exported reference {name} differs from SDK pack {reference_version}")
    print(f"release runtime: {target} + self-contained .NET {version} ({rid}); {len(names)} critical files and {len(actuals)} reference assemblies ({reference_version}) verified")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("rid", choices=["linux-x64", "win-x64"])
    parser.add_argument("--packages", type=Path, default=Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages")))
    parser.add_argument("--expected-release", type=Path, help="require the same core, mod contract, deps and config as the release just built")
    args = parser.parse_args()
    try:
        verify(args.root, args.rid, args.packages, args.expected_release)
        verify_content(args.root)
    except (OSError, ValueError, KeyError, struct.error, ET.ParseError) as error:
        print(f"release runtime verification failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
