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
            var manifest = TestManifest.Load(Path.Combine(root, "tests", "tests.manifest.json"));
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

        private static TestManifest Manifest() => new()
        {
            SchemaVersion = 2, Unit = new UnitManifest { Project = "test.csproj" },
            Integration = new() { new IntegrationTestDefinition { Name = "probe", Scene = "res://probe.tscn", Args = Array.Empty<string>(),
                Result = new ResultContract { Name = "probe" }, Owners = new[] { "core" }, Profiles = new[] { "full" } } },
        };
    }
}
