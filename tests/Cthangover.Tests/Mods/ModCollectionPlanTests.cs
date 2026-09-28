using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests what the collection application plan decides: which mods to
    /// download and at which version (honouring the collection's version pin),
    /// which disabled mods to re-enable, and what blocks a full apply. The plan
    /// is the last pure step before the UI touches the network and
    /// <c>disabled_mods.json</c>.
    /// </summary>
    public class ModCollectionPlanTests
    {
        [Fact]
        public void Build_NullGraph_ReturnsEmptyPlan()
        {
            var plan = ModCollectionPlan.Build(null, new List<CatalogExportEntry>());

            Assert.True(plan.IsEmpty);
            Assert.False(plan.HasProblems);
        }

        [Fact]
        public void Build_EverythingInstalled_PlanIsEmpty()
        {
            var collection = Collection(Mechanic("game", "app"));
            var graph = Resolve(collection, null, Local("app", "1.0"));

            var plan = ModCollectionPlan.Build(graph, new List<CatalogExportEntry>());

            Assert.True(plan.IsEmpty);
            Assert.False(plan.HasProblems);
        }

        [Fact]
        public void Build_CatalogMod_InstallsNewestDownloadFromEntrySource()
        {
            var collection = Collection(Mechanic("game", "app"));
            var catalog = new List<CatalogExportEntry> { Catalog("app", "2.0", "1.0") };
            var graph = Resolve(collection, catalog);

            var plan = ModCollectionPlan.Build(graph, catalog);

            var item = Assert.Single(plan.Install);
            Assert.Equal("app", item.ModId);
            Assert.Equal("2.0", item.Version);
            Assert.Equal("https://catalog.test/catalog.json", item.SourceUrl);
            Assert.False(plan.HasProblems);
        }

        [Fact]
        public void Build_PinnedVersion_ChoosesNewestSatisfyingDownload()
        {
            var collection = Collection(Mechanic("game", "app>=1.5"));
            var catalog = new List<CatalogExportEntry> { Catalog("app", "2.0", "1.0") };
            var graph = Resolve(collection, catalog);

            var plan = ModCollectionPlan.Build(graph, catalog);

            var item = Assert.Single(plan.Install);
            Assert.Equal("2.0", item.Version);
        }

        [Fact]
        public void Build_ExactPin_ChoosesOlderSatisfyingDownload()
        {
            var collection = Collection(Mechanic("game", "app=1.0"));
            var catalog = new List<CatalogExportEntry> { Catalog("app", "2.0", "1.0") };
            var graph = Resolve(collection, catalog);

            var plan = ModCollectionPlan.Build(graph, catalog);

            var item = Assert.Single(plan.Install);
            Assert.Equal("1.0", item.Version);
        }

        [Fact]
        public void Build_PinWithoutSatisfyingDownload_ReportsMismatch()
        {
            var collection = Collection(Mechanic("game", "app>5"));
            var catalog = new List<CatalogExportEntry> { Catalog("app", "2.0", "1.0") };
            var graph = Resolve(collection, catalog);

            var plan = ModCollectionPlan.Build(graph, catalog);

            Assert.Empty(plan.Install);
            Assert.Contains("app", plan.Mismatches);
            Assert.True(plan.HasProblems);
        }

        [Fact]
        public void Build_DisabledMod_GoesToEnableList()
        {
            var collection = Collection(Mechanic("game", "off"));
            var graph = Resolve(collection, null, new HashSet<string> { "off" }, Local("off", "1.0"));

            var plan = ModCollectionPlan.Build(graph, new List<CatalogExportEntry>());

            Assert.Empty(plan.Install);
            Assert.Contains("off", plan.Enable);
            Assert.False(plan.IsEmpty);
            Assert.False(plan.HasProblems);
        }

        [Fact]
        public void Build_MissingMod_IsReportedAsProblem()
        {
            var collection = Collection(Mechanic("game", "gone"));
            var graph = Resolve(collection, null);

            var plan = ModCollectionPlan.Build(graph, new List<CatalogExportEntry>());

            Assert.Contains("gone", plan.Missing);
            Assert.Empty(plan.Install);
            Assert.True(plan.IsEmpty);
            Assert.True(plan.HasProblems);
        }

        [Fact]
        public void Build_ExternalDependencyAlsoHandled()
        {
            var collection = Collection(Mechanic("game", "app"));
            var mods = new IModInfo[] { Local("app", "1.0", "core>=1") };
            var catalog = new List<CatalogExportEntry> { Catalog("app", "1.0"), Catalog("core", "1.0") };
            var graph = Resolve(collection, catalog, mods);

            var plan = ModCollectionPlan.Build(graph, catalog);

            var item = Assert.Single(plan.Install);
            Assert.Equal("core", item.ModId);
        }

        private static ModCollectionGraph Resolve(
            ModCollection collection,
            List<CatalogExportEntry> catalog,
            params IModInfo[] mods)
            => Resolve(collection, catalog, new HashSet<string>(), mods);

        private static ModCollectionGraph Resolve(
            ModCollection collection,
            List<CatalogExportEntry> catalog,
            HashSet<string> disabled,
            params IModInfo[] mods)
        {
            var dictionary = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                dictionary[mod.Id] = mod;

            return ModCollectionResolver.Resolve(collection, dictionary, disabled, catalog ?? new List<CatalogExportEntry>());
        }

        private static ModCollection Collection(params ModCollectionMechanic[] mechanics)
            => new()
            {
                Id = "test",
                Name = "Test",
                Mechanics = new List<ModCollectionMechanic>(mechanics),
            };

        private static ModCollectionMechanic Mechanic(string id, params string[] mods)
            => new() { Id = id, Name = id, Mods = new List<string>(mods) };

        private static ModInfo Local(string id, string version, params string[] depends)
        {
            var manifest = new ModManifest { Id = id, Name = id, Version = version };
            var list = new List<ModDependency>();
            foreach (var dep in depends)
                list.Add(ModDependency.Parse(dep));
            manifest.Depends = list;
            return new ModInfo { Id = id, Name = id, Manifest = manifest };
        }

        private static CatalogExportEntry Catalog(string id, params string[] versions)
        {
            var entry = new CatalogExportEntry
            {
                Id = id,
                Name = id,
                Version = versions.FirstOrDefault(),
                Downloads = new List<CatalogEntryDownload>(),
                SourceUrls = new List<string> { "https://catalog.test/catalog.json" },
            };

            foreach (var version in versions)
            {
                entry.Downloads.Add(new CatalogEntryDownload
                {
                    Version = version,
                    Urls = new List<string> { $"https://example.com/{id}-{version}.zip" },
                });
            }

            return entry;
        }
    }
}
