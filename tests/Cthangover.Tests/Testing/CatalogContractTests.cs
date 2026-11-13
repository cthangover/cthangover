namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.TestRunner;
    using Engine.Tests.Arch;

    /// <summary>Every installed suite and core test scene is scheduled or carries an explicit manual-use reason.</summary>
    public class CatalogContractTests
    {
        [Fact]
        public void RepositoryCatalogueHasContractsAndExplicitDependencyChains()
        {
            var root = RepoPaths.FindRepoRoot();
            var manifest = TestManifest.LoadProject(root);
            var catalog = TestCatalog.Load(root, manifest);
            Assert.All(manifest.Integration, test => Assert.False(string.IsNullOrWhiteSpace(test.Result.Name)));
            Assert.Contains(catalog, test => test.Name == "mod_units:rpg" && test.Kind == "mod-unit");
            Assert.Contains(catalog, test => test.Name == "mod_units:avatar_base__contracts" && test.Args.Contains("--itest=avatar_contracts"));
            Assert.Contains(catalog, test => test.Name == "manual:rpg__snapshots" && !string.IsNullOrWhiteSpace(test.ManualReason));
            Assert.Contains("new_mod", catalog.Single(test => test.Name == "new_mod_check").DependsOn);
            Assert.Contains("tutorial_first_game", catalog.Single(test => test.Name == "tutorial_first_game_play").DependsOn);
            var restored = JsonSerializer.Deserialize<TestManifest>(JsonSerializer.Serialize(manifest, ReportWriter.JsonOptions), ReportWriter.JsonOptions);
            restored.Validate();
            Assert.Equal(JsonSerializer.Serialize(manifest, ReportWriter.JsonOptions), JsonSerializer.Serialize(restored, ReportWriter.JsonOptions));
        }

        [Fact]
        public void DuplicateNamesAndMissingContractsAreRejected()
        {
            var manifest = Manifest();
            manifest.Integration.Add(new IntegrationTestDefinition { Name = "probe" });
            Assert.Throws<InvalidOperationException>(manifest.Validate);
            manifest.Integration.RemoveAt(1);
            manifest.Integration[0].Result = null;
            Assert.Throws<InvalidOperationException>(manifest.Validate);
        }

        [Fact]
        public void MissingAndCyclicDependenciesAreRejected()
        {
            var manifest = Manifest();
            manifest.Integration[0].DependsOn = new[] { "missing" };
            Assert.Throws<InvalidOperationException>(manifest.Validate);
            manifest.Integration[0].DependsOn = new[] { "probe" };
            Assert.Throws<InvalidOperationException>(manifest.Validate);
        }

        [Fact]
        public void IntegrationCanRequireTheUnitStage()
        {
            var manifest = Manifest();
            manifest.Integration[0].DependsOn = new[] { "unit" };
            manifest.Validate();
        }

        [Fact]
        public void ModsFolderPartJoinsTheProjectManifestAndItsModsAreCatalogued()
        {
            var root = Path.Combine(Path.GetTempPath(), "manifest_parts_" + Guid.NewGuid().ToString("N"));
            try
            {
                var folder = ModFolders.Names.First(name => name != ModFolders.Primary);
                Directory.CreateDirectory(Path.Combine(root, "tests"));
                Directory.CreateDirectory(Path.Combine(root, folder, "hidden", "test_suites", "walk"));
                File.WriteAllText(Path.Combine(root, "tests", ModFolders.ManifestFile), JsonSerializer.Serialize(Manifest(), ReportWriter.JsonOptions));
                File.WriteAllText(Path.Combine(root, folder, "hidden", "manifest.json"),
                    "{\"id\":\"hidden\",\"test_sources\":[\"tests/*.cs\"],\"test_suites\":[{\"id\":\"walk\",\"activate\":[\"--itest=hidden_walk\"]},{\"id\":\"manual\",\"activate\":[\"--itest=hidden_manual\"]}]}");
                File.WriteAllText(Path.Combine(root, folder, "hidden", "test_suites", "walk", "manifest.json"), "{\"id\":\"walk\"}");
                Directory.CreateDirectory(Path.Combine(root, folder, "hidden", "test_suites", "manual"));
                File.WriteAllText(Path.Combine(root, folder, "hidden", "test_suites", "manual", "manifest.json"), "{\"id\":\"manual\"}");
                var part = new TestManifest
                {
                    Integration = new() { new IntegrationTestDefinition { Name = "hidden_walk", Scene = "res://probe.tscn", Args = new[] { "--itest=hidden_walk" },
                        Result = new ResultContract { Name = "hidden_walk" }, Owners = new[] { "hidden" }, Profiles = new[] { "full" }, DependsOn = new[] { "probe" } } },
                    ManualSuites = new() { ["hidden__manual"] = new ManualTestPolicy { Reason = "Run by hand." } },
                };
                File.WriteAllText(Path.Combine(root, folder, ModFolders.ManifestFile), JsonSerializer.Serialize(part, ReportWriter.JsonOptions));

                var manifest = TestManifest.LoadProject(root);
                var catalog = TestCatalog.Load(root, manifest);

                Assert.Equal(new[] { "probe", "hidden_walk" }, manifest.Integration.Select(test => test.Name));
                Assert.Contains(catalog, test => test.Name == "hidden_walk" && test.Owners.Contains("hidden"));
                Assert.Contains(catalog, test => test.Name == "mod_units:hidden" && test.Kind == "mod-unit");
                Assert.Contains(catalog, test => test.Name == "manual:hidden__manual" && test.ManualReason == "Run by hand.");

                File.WriteAllText(Path.Combine(root, folder, ModFolders.ManifestFile), JsonSerializer.Serialize(Manifest(), ReportWriter.JsonOptions));
                Assert.Throws<InvalidOperationException>(() => TestManifest.LoadProject(root));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static TestManifest Manifest() => new()
        {
            SchemaVersion = 2, Unit = new UnitManifest { Project = "test.csproj" },
            Integration = new() { new IntegrationTestDefinition { Name = "probe", Scene = "res://probe.tscn", Args = Array.Empty<string>(),
                Result = new ResultContract { Name = "probe" }, Owners = new[] { "core" }, Profiles = new[] { "full" } } },
        };
    }
}
