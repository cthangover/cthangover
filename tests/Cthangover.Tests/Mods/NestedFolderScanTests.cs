using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Compilation;
using Cthangover.Core.Mods.Resolvers;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// A host's own scans must not read the folders of its nested mods (test suites, integrations): they are
    /// mounted as virtual mods <c>&lt;host&gt;__&lt;id&gt;</c> with their own files. An entity inside a suite folder
    /// joins the host only when the suite is mounted, and a host without sources never compiles suite code.
    /// </summary>
    public class NestedFolderScanTests
    {
        private const string SuiteEntity = "test_suites/walk/placements/rpg_village/rpg_walk_test.json";

        [Fact]
        public void EntityScan_HostWithSuiteFolder_DoesNotExposeTheSuiteEntity()
        {
            var host = Host();

            var paths = EntityResolver.JsonPathsOf(host.FileProvider, host.Manifest);

            Assert.Contains("items/sword.json", paths);
            Assert.DoesNotContain(SuiteEntity, paths);
            Assert.DoesNotContain(paths, path => path.StartsWith("test_suites/", StringComparison.Ordinal));
        }

        [Fact]
        public void EntityScan_UndeclaredSuiteFolder_IsHidden()
        {
            // A folder under test_suites/ that the manifest does not declare is not mounted, but it is still not the host's.
            var host = Host();
            host.Manifest.TestSuites = null;

            var paths = EntityResolver.JsonPathsOf(host.FileProvider, host.Manifest);

            Assert.Contains("items/sword.json", paths);
            Assert.DoesNotContain(SuiteEntity, paths);
        }

        [Fact]
        public void Listing_UndeclaredNestedFolders_AreHiddenFromTheHost()
        {
            // The listing the editors and locale atlas use: test_suites/ and integrations/ belong to nested mods, declared or not.
            var provider = new MockModFileProvider("rpg");
            provider.AddFile("items/sword.json", "{}");
            provider.AddFile("test_suites/stray/manifest.json", "{}");
            provider.AddFile("integrations/battles/manifest.json", "{}");
            var manifest = new ModManifest { Id = "rpg", Version = "0.1.0" };

            var listed = NestedFolders.Visible(provider, manifest).ListFiles(string.Empty).ToList();

            Assert.Contains("items/", listed);
            Assert.DoesNotContain(listed, entry => entry.StartsWith("test_suites", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(listed, entry => entry.StartsWith("integrations", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EntityScan_MountedSuite_ExposesItsOwnEntities()
        {
            var host = Host();
            var mods = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase) { [host.Id] = host };

            ModTestSuites.Mount(mods, new[] { "--itest=walk" });

            var suite = (ModInfo)mods["rpg__walk"];
            var paths = EntityResolver.JsonPathsOf(suite.FileProvider, suite.Manifest);
            Assert.Contains("placements/rpg_village/rpg_walk_test.json", paths);
        }

        [Fact]
        public void SourceFallback_HostWithoutSources_DoesNotCompileSuiteSources()
        {
            var host = Host();
            host.Manifest.Sources = new List<string> { "src/*.cs" };
            ((MockModFileProvider)host.FileProvider).AddFile("test_suites/walk/src/Probe.cs", "class Probe {}");

            var sources = ModSourceCollector.CollectSourceFiles(host, CancellationToken.None);

            Assert.DoesNotContain(sources, source => source.FilePath.StartsWith("test_suites/", StringComparison.Ordinal));
            Assert.Empty(sources.Where(source => source.FilePath.EndsWith("Probe.cs", StringComparison.Ordinal)));
        }

        /// <summary>A host with one entity in its own folder and one entity in the folder of its test suite <c>walk</c>.</summary>
        private static ModInfo Host()
        {
            var provider = new MockModFileProvider("rpg");
            provider.AddFile("items/sword.json", """{ "Kind": "item", "Items": [ { "Id": "sword" } ] }""");
            provider.AddFile(SuiteEntity, """{ "Kind": "rpg_placement", "Items": [ { "Id": "rpg_walk/mushroom" } ] }""");
            provider.AddFile("test_suites/walk/manifest.json", """{ "id": "walk" }""");
            return new ModInfo
            {
                Id = "rpg",
                Name = "RPG",
                FileProvider = provider,
                Manifest = new ModManifest
                {
                    Id = "rpg",
                    Version = "0.1.0",
                    TestSuites = new List<ModTestSuite>
                    {
                        new() { Id = "walk", Activate = new List<string> { "--itest=walk" } },
                    },
                },
            };
        }
    }
}
