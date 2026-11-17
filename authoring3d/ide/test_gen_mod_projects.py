"""Contract checks for manifest-to-project generation, without launching the game."""

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET


SPEC = importlib.util.spec_from_file_location("gen_mod_projects", Path(__file__).with_name("gen_mod_projects.py"))
GEN = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = GEN
SPEC.loader.exec_module(GEN)


class ModProjectsTests(unittest.TestCase):
    """The project graph keeps runtime compilation boundaries and owns only generated files."""

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="cthangover_ide_")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.root.joinpath("Cthangover.csproj").write_text(
            '<Project Sdk="Godot.NET.Sdk/4.6.3"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<LangVersion>12</LangVersion></PropertyGroup></Project>', encoding="utf-8")

    def manifest(self, folder, data):
        path = self.root / folder
        path.mkdir(parents=True, exist_ok=True)
        path.joinpath("manifest.json").write_text(json.dumps(data), encoding="utf-8")
        return path

    def test_nested_dependencies_tests_and_globs(self):
        host = self.manifest("mods/host", {"id": "host", "sources": ["src/*.cs"], "test_sources": ["tests/*.cs"],
            "integrations": [{"id": "glue", "with": ["partner"], "path": "glue"}],
            "test_suites": [{"id": "probe"}]})
        self.manifest("mods/partner", {"id": "partner"})
        self.manifest("mods/host/glue", {"sources": ["src/**/*.cs"], "depends": ["partner>=1?"]})
        self.manifest("mods/host/test_suites/probe", {"test_sources": ["tests/*.cs"]})
        result = GEN.update(self.root)
        self.assertEqual(6, result["projects"])
        production = ET.parse(host / "host.csproj")
        self.assertEqual("src/**/*.cs", production.find(".//Compile").attrib["Include"])
        self.assertNotIn("tests/*.cs", host.joinpath("host.csproj").read_text())
        self.assertEqual("tests/**/*.cs", ET.parse(host / "host__tests.csproj").find(".//Compile").attrib["Include"])
        glue = ET.parse(host / "glue/host__glue.csproj")
        refs = [item.attrib["Include"] for item in glue.findall(".//ProjectReference")]
        self.assertTrue(any(value.endswith("host.csproj") for value in refs))
        self.assertTrue(any(value.endswith("partner.csproj") for value in refs))
        self.assertIn("glue/**", production.find(".//Compile").attrib["Exclude"])

    def test_idempotent_and_new_source_is_wildcard(self):
        mod = self.manifest("mods_private/actor", {"id": "actor", "sources": ["src/**/*.cs"]})
        GEN.update(self.root)
        project = mod / "actor.csproj"
        stamp = project.stat().st_mtime_ns
        mod.joinpath("src").mkdir()
        mod.joinpath("src/New.cs").write_text("public class New {}")
        second = GEN.update(self.root)
        self.assertEqual([], second["added"])
        self.assertEqual([], second["updated"])
        self.assertEqual(stamp, project.stat().st_mtime_ns)
        self.assertIn("src/**/*.cs", project.read_text())
        self.assertNotIn("New.cs", project.read_text())

    def test_removed_mod_project_is_removed_but_human_project_survives(self):
        mod = self.manifest("mods/obsolete", {"id": "obsolete"})
        GEN.update(self.root)
        mod.joinpath("manifest.json").unlink()
        mod.joinpath("Human.csproj").write_text("<Project />")
        result = GEN.update(self.root)
        self.assertEqual(["mods/obsolete/obsolete.csproj"], result["removed"])
        self.assertTrue(mod.joinpath("Human.csproj").is_file())
        result = GEN.update(self.root)
        self.assertEqual([], result["removed"])
        self.assertTrue(mod.joinpath("Human.csproj").exists())

    def test_paths_outputs_managed_libraries_and_fallback(self):
        mod = self.manifest("mods/api", {"id": "api", "references": ["lib/**/*.dll"], "depends": ["absent?"]})
        GEN.update(self.root)
        project = ET.parse(mod / "api.csproj")
        self.assertEqual("$(MSBuildThisFileDirectory)../../", project.findtext(".//CthangoverRoot"))
        self.assertIn(".ide/obj/api/", project.findtext(".//BaseIntermediateOutputPath"))
        self.assertIn(".ide/bin/api/", project.findtext(".//OutputPath"))
        refs = [item.attrib["Include"] for item in project.findall(".//Reference")]
        self.assertIn("lib/**/*.dll", refs)
        self.assertIn("Cthangover", refs)
        self.assertEqual("false", project.findtext(".//EnableDefaultCompileItems"))
        self.assertEqual("", project.findall(".//DefineConstants")[-1].text or "")
        analyzer = project.find(".//Target[@Name='UseRuntimeModGenerators']/ItemGroup/Analyzer")
        self.assertEqual("'%(Analyzer.Filename)' != 'Godot.SourceGenerators'", analyzer.attrib["Condition"])
        self.assertIsNone(project.find(".//Error"))

    def test_duplicate_id_and_cycles_fail_before_writing(self):
        self.manifest("mods/a", {"id": "a", "depends": ["b"]})
        self.manifest("mods/b", {"id": "b", "depends": ["a"]})
        with self.assertRaisesRegex(ValueError, "cycle"):
            GEN.update(self.root)
        self.assertFalse(self.root.joinpath("mods/a/a.csproj").exists())
        self.root.joinpath("mods/b/manifest.json").write_text('{"id":"a"}')
        with self.assertRaisesRegex(ValueError, "duplicate"):
            GEN.update(self.root)

    def test_human_project_cannot_be_overwritten(self):
        mod = self.manifest("mods/host", {"id": "host"})
        mod.joinpath("host.csproj").write_text("<Project />")
        with self.assertRaisesRegex(ValueError, "handwritten"):
            GEN.update(self.root)
        self.assertEqual("<Project />", mod.joinpath("host.csproj").read_text())

    def test_source_project_outputs_and_release_binary_fallback(self):
        mod = self.manifest("mods/library", {"id": "library"})
        source = mod / "source_code"
        source.mkdir()
        source.joinpath("Api.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><AssemblyName>Api</AssemblyName></PropertyGroup></Project>')
        mod.joinpath("dll").mkdir()
        mod.joinpath("dll/Api.dll").write_bytes(b"fixture")
        GEN.update(self.root)
        project = ET.parse(mod / "library.csproj")
        refs = project.findall(".//ProjectReference")
        self.assertEqual("Exists('source_code/Api.csproj')", refs[-1].attrib["Condition"])
        self.assertIsNotNone(project.find(".//CthangoverSourceBinaryExcludes"))
        props = ET.parse(source / "Directory.Build.props")
        self.assertIn(".ide/obj/source_library/", props.findtext(".//BaseIntermediateOutputPath"))
        self.assertIn("obj/**", props.findtext(".//DefaultItemExcludes"))
        self.assertEqual([], GEN.update(self.root)["updated"])

    def test_test_visibility_does_not_leak_into_production(self):
        host = self.manifest("mods/host", {"id": "host", "sources": ["src/*.cs"], "test_sources": ["tests/*.cs"]})
        self.manifest("mods/api", {"id": "api", "sources": ["src/*.cs"]})
        self.manifest("mods_private/hidden", {"id": "hidden", "fixture": True, "sources": ["src/*.cs"]})
        GEN.update(self.root)
        production = host.joinpath("host.csproj").read_text()
        tests = host.joinpath("host__tests.csproj").read_text()
        self.assertNotIn("api.csproj", production)
        self.assertIn("api.csproj", tests)
        self.assertNotIn("hidden.csproj", tests)

    def test_managed_dependency_references_follow_transitive_closure(self):
        self.manifest("mods/api", {"id": "api", "references": ["lib/*.dll"]})
        self.manifest("mods/middle", {"id": "middle", "depends": ["api"]})
        consumer = self.manifest("mods_private/consumer", {"id": "consumer", "depends": ["middle"]})
        GEN.update(self.root)
        refs = ET.parse(consumer / "consumer.csproj").findall(".//Reference")
        self.assertTrue(any(item.attrib["Include"] == "../../mods/api/lib/*.dll" for item in refs))


if __name__ == "__main__":
    unittest.main()
