using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the collection graph resolution rules: mechanics group the declared
    /// mods, <c>depends</c> produce the arrows and pull transitive dependencies
    /// into the graph as external nodes, and every node's state reflects the
    /// three live sources (local registry, disabled store, catalog cache).
    /// </summary>
    public class ModCollectionResolverTests
    {
        [Fact]
        public void Resolve_NullCollection_ReturnsEmptyGraph()
        {
            var graph = ModCollectionResolver.Resolve(
                null,
                new Dictionary<string, IModInfo>(),
                new HashSet<string>(),
                new List<CatalogExportEntry>());

            Assert.NotNull(graph);
            Assert.Empty(graph.Nodes);
            Assert.Empty(graph.Edges);
            Assert.Empty(graph.Mechanics);
        }

        [Fact]
        public void Resolve_GroupsNodesByMechanicInDeclarationOrder()
        {
            var collection = Collection(
                Mechanic("core", "app"),
                Mechanic("ui", "skin"));
            var mods = Mods(Local("app", "1.0"), Local("skin", "1.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal(2, graph.Nodes.Count);
            Assert.Equal("app", graph.Nodes[0].ModId);
            Assert.Equal("core", graph.Nodes[0].MechanicId);
            Assert.Equal("skin", graph.Nodes[1].ModId);
            Assert.Equal("ui", graph.Nodes[1].MechanicId);
            Assert.False(graph.Nodes[0].IsExternal);
            Assert.Equal(2, graph.InstalledCount);
        }

        [Fact]
        public void Resolve_DependencyOfDeclaredMod_BecomesExternalNodeWithEdge()
        {
            var collection = Collection(Mechanic("game", "app"));
            var mods = Mods(
                Local("app", "1.0", "core>=1"),
                Local("core", "2.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            var core = Node(graph, "core");
            Assert.True(core.IsExternal);
            Assert.Null(core.MechanicId);
            Assert.Single(graph.Edges);
            Assert.Equal("core", graph.Edges[0].FromModId);
            Assert.Equal("app", graph.Edges[0].ToModId);
        }

        [Fact]
        public void Resolve_TransitiveDependencies_ArePulledInRecursively()
        {
            var collection = Collection(Mechanic("game", "top"));
            var mods = Mods(
                Local("top", "1.0", "middle"),
                Local("middle", "1.0", "base"),
                Local("base", "1.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal(3, graph.Nodes.Count);
            Assert.Contains(graph.Edges, e => e.FromModId == "middle" && e.ToModId == "top");
            Assert.Contains(graph.Edges, e => e.FromModId == "base" && e.ToModId == "middle");
        }

        [Fact]
        public void Resolve_StatesReflectLocalDisabledCatalogAndMissing()
        {
            var collection = Collection(Mechanic("game", "on", "off", "remote", "gone"));
            var mods = Mods(Local("on", "1.0"), Local("off", "1.0"));
            var catalog = new List<CatalogExportEntry> { Catalog("remote", "1.0") };

            var graph = ModCollectionResolver.Resolve(
                collection, mods, new HashSet<string> { "off" }, catalog);

            Assert.Equal(ModCollectionNodeState.Installed, Node(graph, "on").State);
            Assert.Equal(ModCollectionNodeState.Disabled, Node(graph, "off").State);
            Assert.Equal(ModCollectionNodeState.InCatalog, Node(graph, "remote").State);
            Assert.Equal(ModCollectionNodeState.Missing, Node(graph, "gone").State);
            Assert.Equal(1, graph.InstalledCount);
            Assert.Equal(4, graph.TotalCount);
        }

        [Fact]
        public void Resolve_PinnedVersionNotSatisfied_ReportsMismatch()
        {
            var collection = Collection(Mechanic("game", "app>=2"));
            var mods = Mods(Local("app", "1.5"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "app").State);
            Assert.Equal(">=2", Node(graph, "app").RequiredVersion);
        }

        [Fact]
        public void Resolve_PinnedVersionSatisfied_ReportsInstalled()
        {
            var collection = Collection(Mechanic("game", "app>=1"));
            var mods = Mods(Local("app", "1.5"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal(ModCollectionNodeState.Installed, Node(graph, "app").State);
        }

        [Fact]
        public void Resolve_SessionInstalled_TreatedAsInstalled()
        {
            var collection = Collection(Mechanic("game", "app"));
            var session = new HashSet<string> { "app" };

            var graph = ModCollectionResolver.Resolve(
                collection,
                new Dictionary<string, IModInfo>(),
                new HashSet<string>(),
                new List<CatalogExportEntry>(),
                session);

            Assert.Equal(ModCollectionNodeState.Installed, Node(graph, "app").State);
        }

        [Fact]
        public void Resolve_ModDeclaredByTwoMechanics_BelongsToFirst()
        {
            var collection = Collection(
                Mechanic("first", "shared"),
                Mechanic("second", "shared"));
            var mods = Mods(Local("shared", "1.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Single(graph.Nodes);
            Assert.Equal("first", Node(graph, "shared").MechanicId);
        }

        [Fact]
        public void Resolve_UnnamedMechanic_GetsStableFallbackId()
        {
            var collection = Collection(new ModCollectionMechanic
            {
                Mods = new List<string> { "app" },
            });
            var mods = Mods(Local("app", "1.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal("mechanic_0", graph.Mechanics[0].Id);
            Assert.Equal("mechanic_0", Node(graph, "app").MechanicId);
        }

        [Fact]
        public void Resolve_InvalidVersionedModString_UsesRawId()
        {
            var collection = Collection(Mechanic("game", "!!!not-a-mod"));
            var mods = Mods(Local("!!!not-a-mod", "1.0"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Single(graph.Nodes);
            Assert.Equal("!!!not-a-mod", graph.Nodes[0].ModId);
            Assert.Equal(ModCollectionNodeState.Installed, graph.Nodes[0].State);
        }

        private static ModCollectionNode Node(ModCollectionGraph graph, string modId)
            => graph.Nodes.First(n => n.ModId == modId);

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

        private static Dictionary<string, IModInfo> Mods(params ModInfo[] mods)
        {
            var result = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                result[mod.Id] = mod;
            return result;
        }

        private static CatalogExportEntry Catalog(string id, params string[] versions)
        {
            var entry = new CatalogExportEntry
            {
                Id = id,
                Name = id,
                Version = versions.Length > 0 ? versions[0] : null,
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
