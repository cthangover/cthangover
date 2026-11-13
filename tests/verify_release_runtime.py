#!/usr/bin/env python3
"""Verify the exported CLR against its resolved runtime pack before archiving.

Проверка экспортированного CLR по runtime pack перед упаковкой релиза.
SDK 10/net10.0 compile the game; the self-contained payload includes CLR 10
and the SDK reference pack needed to compile mods without an installed SDK.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET


def verify(root, rid, packages):
    project = ET.parse(Path(__file__).resolve().parents[1] / "Cthangover.csproj")
    version = project.findtext(".//ReleaseRuntimeVersion")
    target = project.findtext(".//TargetFramework")
    if target != "net10.0" or not version or not version.startswith("10."):
        raise ValueError("the release contract requires net10.0 and a CLR 10 servicing version")
    configs = list(root.rglob("Cthangover.runtimeconfig.json"))
    if len(configs) != 1:
        raise ValueError(f"expected one game runtimeconfig, found {len(configs)}")
    folder = configs[0].parent
    if not (folder / "Cthangover.dll").is_file():
        raise ValueError("exported game assembly is missing")
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
    args = parser.parse_args()
    try:
        verify(args.root, args.rid, args.packages)
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        print(f"release runtime verification failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
