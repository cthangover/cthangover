#!/usr/bin/env python3
"""Build generated contexts and prove wildcard, DLL, dependency and installed-core behavior."""

import argparse
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


SPEC = importlib.util.spec_from_file_location("gen_mod_projects", Path(__file__).with_name("gen_mod_projects.py"))
GEN = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = GEN
SPEC.loader.exec_module(GEN)


def run(command, output, expected_success=True):
    with output.open("w", encoding="utf-8") as stream:
        result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, text=True)
    text = output.read_text(encoding="utf-8", errors="replace")
    if (result.returncode == 0) != expected_success:
        raise RuntimeError("Unexpected exit " + str(result.returncode) + ": " + str(output) + "\n" + text[-6000:])
    return text


def write_manifest(folder, data):
    folder.mkdir(parents=True, exist_ok=True)
    folder.joinpath("manifest.json").write_text(json.dumps(data, indent=2), encoding="utf-8")


def bin_obj_directories(root):
    result = set()
    for group in ("mods", "mods_private"):
        for directory, children, _ in GEN.os.walk(root / group):
            children[:] = [name for name in children if not name.startswith(".")]
            for name in children:
                if name in ("bin", "obj"):
                    result.add(str(Path(directory) / name))
            children[:] = [name for name in children if name not in ("bin", "obj")]
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--solution", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    before = bin_obj_directories(root)
    first = GEN.update(root)
    second = GEN.update(root)
    assert not second["added"] and not second["updated"] and not second["removed"], second
    out.joinpath("generation.json").write_text(json.dumps(first, indent=2))
    out.joinpath("generation-repeat.json").write_text(json.dumps(second, indent=2))
    fixture = root / ".ide/acceptance"
    if fixture.exists():
        shutil.rmtree(fixture)
    fixture.mkdir(parents=True)
    core = ET.Element("Project", {"Sdk": "Godot.NET.Sdk/4.6.3"})
    props = ET.SubElement(core, "PropertyGroup")
    for name, value in {"TargetFramework": "net10.0", "LangVersion": "12", "EnableDefaultCompileItems": "false",
                        "AssemblyName": "IdeAcceptanceKernel"}.items():
        ET.SubElement(props, name).text = value
    ET.SubElement(ET.SubElement(core, "ItemGroup"), "ProjectReference", {"Include": str(root / "Cthangover.csproj")})
    ET.ElementTree(core).write(fixture / "Cthangover.csproj", encoding="unicode")
    api = fixture / "mods/api"
    consumer = fixture / "mods_private/consumer"
    write_manifest(api, {"id": "api", "sources": ["src/**/*.cs"], "references": ["lib/*.dll"]})
    write_manifest(consumer, {"id": "consumer", "sources": ["src/**/*.cs"]})
    api.joinpath("src").mkdir()
    consumer.joinpath("src").mkdir()
    api.joinpath("src/Api.cs").write_text(
        "public static class IdeOwnedApi { public static int Read() => IdeManagedApi.Read(); "
        "public static System.Type CoreType => typeof(Cthangover.Core.Mods.ModManifest); }", encoding="utf-8")
    api.joinpath("src/Forwarder.cs").write_text("public class IdeForwarder : Godot.Node { }", encoding="utf-8")
    consumer.joinpath("src/Consumer.cs").write_text(
        "public static class IdeConsumer { public static int Read() => IdeOwnedApi.Read() + IdeManagedApi.Read(); }",
        encoding="utf-8")
    library = fixture / "library_input"
    library.mkdir()
    library.joinpath("IdeManagedApi.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
        '</PropertyGroup></Project>', encoding="utf-8")
    library.joinpath("Api.cs").write_text(
        "public static class IdeManagedApi { public static int Read() => 7; }", encoding="utf-8")
    run(["dotnet", "build", str(library / "IdeManagedApi.csproj"), "-o", str(api / "lib"), "-v:quiet"],
        out / "fixture-library.log")
    GEN.update(fixture)
    failed = run(["dotnet", "build", str(consumer / "consumer.csproj"), "-v:quiet"],
                 out / "undeclared-dependency.log", False)
    assert "CS0103" in failed or "CS0246" in failed, failed[-3000:]
    write_manifest(consumer, {"id": "consumer", "sources": ["src/**/*.cs"], "depends": ["api"]})
    GEN.update(fixture)
    run(["dotnet", "build", str(consumer / "consumer.csproj"), "-v:quiet"], out / "declared-dependency.log")
    new_source = api / "src/AddedAfterGeneration.cs"
    new_source.write_text("public static class IdeAddedLater { public static int Read() => 19; }", encoding="utf-8")
    items = run(["dotnet", "msbuild", str(api / "api.csproj"), "-getItem:Compile"], out / "wildcard-items.json")
    assert "AddedAfterGeneration.cs" in items
    run(["dotnet", "build", str(api / "api.csproj"), "-v:quiet"], out / "wildcard-build.log")
    installed = fixture / "installed"
    installed.mkdir()
    core_output = root / ".godot/mono/temp/bin/Debug"
    run(["dotnet", "build", str(api / "api.csproj"), "-v:quiet",
         "-p:CthangoverRoot=" + str(installed) + "/",
         "-p:CthangoverInstalledDirectory=" + str(core_output) + "/"], out / "installed-core-build.log")
    after = bin_obj_directories(root)
    assert not after.difference(before), sorted(after.difference(before))
    assert not bin_obj_directories(fixture), bin_obj_directories(fixture)
    result = {"projects": second["projects"], "idempotent": True, "wildcard": True,
              "managedDll": True, "dependencyDeniedThenDeclared": True, "standaloneInstalledCore": True,
              "newModBinObjDirectories": [], "solutionBuild": False}
    out.joinpath("acceptance.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    if args.solution:
        run(["dotnet", "build", str(root / "Cthangover.All.sln"), "-maxcpucount:2", "-v:quiet",
             "-p:UseSharedCompilation=false"], out / "solution-build.log")
        assert not bin_obj_directories(root).difference(before)
        result["solutionBuild"] = True
        out.joinpath("acceptance.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
