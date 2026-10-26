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
        public void Resolve_RepeatedModWithinOneGroup_RetainsEveryVersionConstraint()
        {
            var collection = Collection(Mechanic("game", "shared>=1", "shared>=2"));
            var mods = Mods(Local("shared", "1.5"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Single(graph.Nodes);
            Assert.Single(graph.Groups[0].Mods);
            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "shared").State);
            Assert.True(Node(graph, "shared").AcceptsVersion("2"));
        }

        [Fact]
        public void Resolve_SharedMod_MustSatisfyBothMechanicsRequirements()
        {
            var collection = Collection(Mechanic("first", "shared>=1"), Mechanic("second", "shared>=2"));
            var mods = Mods(Local("shared", "1.5"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Single(graph.Nodes);
            Assert.Equal("first", Node(graph, "shared").MechanicId);
            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "shared").State);
            Assert.False(Node(graph, "shared").AcceptsVersion("1.5"));
            Assert.True(Node(graph, "shared").AcceptsVersion("2.0"));
        }

        [Fact]
        public void Resolve_ExplicitPin_DoesNotDiscardConsumersStricterDependency()
        {
            var collection = Collection(Mechanic("game", "base>=1", "app"));
            var mods = Mods(Local("base", "1.5"), Local("app", "1", "base>=2"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "base").State);
            Assert.True(Node(graph, "base").AcceptsVersion("2.1"));
        }

        [Fact]
        public void Resolve_ExternalDependency_ValidatesTheRequiredVersion()
        {
            var collection = Collection(Mechanic("game", "app"));
            var mods = Mods(Local("app", "1", "base>=2"), Local("base", "1.5"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.True(Node(graph, "base").IsExternal);
            Assert.Equal(">=2", Node(graph, "base").RequiredVersion);
            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "base").State);
        }

        [Fact]
        public void Resolve_DisabledIncompatibleMod_CannotBeFixedByOnlyEnablingIt()
        {
            var collection = Collection(Mechanic("game", "app>=2"));
            var mods = Mods(Local("app", "1"));

            var graph = ModCollectionResolver.Resolve(collection, mods, new HashSet<string> { "app" }, new List<CatalogExportEntry>());

            Assert.Equal(ModCollectionNodeState.VersionMismatch, Node(graph, "app").State);
        }

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

        [Fact]
        public void Resolve_Include_ExpandsChildMechanicsIntoOneGroup()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude> { new() { Ref = "rpg_2d", Role = "systems" } },
            };
            var child = new ModCollection
            {
                Id = "rpg_2d",
                Name = "2D RPG",
                Description = "2D systems",
                Mechanics = new List<ModCollectionMechanic>
                {
                    new() { Id = "core", Name = "Core", Role = "foundation", Mods = new List<string> { "base_2d" } },
                    new() { Id = "chars", Name = "Chars", Role = "characters", Mods = new List<string> { "character_data" } },
                },
            };
            var mods = Mods(Local("base_2d", "1.0"), Local("character_data", "1.0"));

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, child), mods, new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Empty(graph.Problems);
            var group = Assert.Single(graph.Groups);
            Assert.True(group.IsInclude);
            Assert.Equal("rpg_2d", group.CollectionId);
            Assert.Equal("systems", group.Role);
            Assert.Equal(2, group.Mods.Count);
            Assert.Equal("base_2d", group.Mods[0].ModId);
            Assert.Equal("core", group.Mods[0].MechanicId);
            Assert.Contains(graph.Nodes, n => n.ModId == "base_2d" && n.MechanicId == "core");
            Assert.Contains(graph.Nodes, n => n.ModId == "character_data");
        }

        [Fact]
        public void Resolve_IncludeLabelAndNote_OverrideGroupText()
        {
            var game = new ModCollection
            {
                Id = "game",
                Includes = new List<ModCollectionInclude>
                {
                    new() { Ref = "rpg_2d", Role = "systems", Label = "Основа", Note = "Подпись игры" },
                },
            };
            var child = new ModCollection { Id = "rpg_2d", Name = "2D RPG", Description = "Original" };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, child), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            var group = Assert.Single(graph.Groups);
            Assert.Equal("Основа", group.Name);
            Assert.Equal("Подпись игры", group.Description);
        }

        [Fact]
        public void Resolve_MissingInclude_ReportsProblemAndSkipsGroup()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude> { new() { Ref = "gone>=1", Role = "systems" } },
            };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Empty(graph.Groups);
            var problem = Assert.Single(graph.Problems);
            Assert.Equal(ModCollectionProblemKind.MissingCollection, problem.Kind);
            Assert.Equal("game", problem.ParentCollectionId);
            Assert.Equal("gone>=1", problem.Ref);
        }

        [Fact]
        public void Resolve_CycleInclude_ReportsProblem()
        {
            var a = new ModCollection
            {
                Id = "a",
                Includes = new List<ModCollectionInclude> { new() { Ref = "b", Role = "x" } },
            };
            var b = new ModCollection
            {
                Id = "b",
                Includes = new List<ModCollectionInclude> { new() { Ref = "a", Role = "y" } },
            };

            var graph = ModCollectionResolver.Resolve(
                a, Collections(a, b), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Contains(graph.Problems, p => p.Kind == ModCollectionProblemKind.Cycle);
            Assert.Single(graph.Groups);
        }

        [Fact]
        public void Resolve_TwoRequiredIncludesSameRole_ReportsConflict()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude>
                {
                    new() { Ref = "story_a", Role = "story" },
                    new() { Ref = "story_b", Role = "story" },
                },
            };
            var a = new ModCollection { Id = "story_a", Name = "Story A" };
            var b = new ModCollection { Id = "story_b", Name = "Story B" };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, a, b), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            var problem = Assert.Single(graph.Problems);
            Assert.Equal(ModCollectionProblemKind.RoleConflict, problem.Kind);
        }

        [Fact]
        public void Resolve_OptionalIncludeOff_StaysAsInactiveGroup()
        {
            var game = new ModCollection
            {
                Id = "game",
                Includes = new List<ModCollectionInclude>
                {
                    new() { Ref = "voice_ru", Role = "locale", Optional = true, Selected = false },
                },
            };
            var child = new ModCollection
            {
                Id = "voice_ru",
                Name = "Voice",
                Mechanics = new List<ModCollectionMechanic>
                {
                    new() { Id = "voice", Mods = new List<string> { "voice_ru" } },
                },
            };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, child), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            var group = Assert.Single(graph.Groups);
            Assert.False(group.Selected);
            Assert.Empty(group.Mods);
            Assert.Empty(graph.Nodes);
        }

        [Fact]
        public void Resolve_OptionalMechanicOff_StaysAsDimmedGroupWithoutNodes()
        {
            var collection = Collection(
                Mechanic("core", "app"),
                Mechanic("extra", "skin"));
            collection.Mechanics[1].Optional = true;
            collection.Mechanics[1].Selected = false;
            var mods = Mods(Local("app", "1.0"), Local("skin", "1.0"));

            var graph = ModCollectionResolver.Resolve(
                collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            // The turned-off contour must stay on the canvas (dimmed) so the
            // player can re-enable it, without its mods joining the graph or
            // the install plan.
            Assert.Equal(2, graph.Groups.Count);
            var off = graph.Groups.Single(g => g.MechanicId == "extra");
            Assert.False(off.Selected);
            Assert.True(off.Optional);
            Assert.Empty(off.Mods);
            Assert.DoesNotContain(graph.Nodes, n => n.ModId == "skin");
            Assert.Single(graph.Nodes);

            var plan = ModCollectionPlan.Build(graph, new List<CatalogExportEntry>());
            Assert.Empty(plan.Install);
            Assert.Empty(plan.Enable);
        }

        [Fact]
        public void Resolve_OptionalMechanicReenabled_GroupRegainsMods()
        {
            var collection = Collection(Mechanic("extra", "skin"));
            collection.Mechanics[0].Optional = true;
            var mods = Mods(Local("skin", "1.0"));

            var graph = ModCollectionResolver.Resolve(
                collection, mods, new HashSet<string>(), new List<CatalogExportEntry>());

            var group = Assert.Single(graph.Groups);
            Assert.True(group.Selected);
            Assert.Single(group.Mods);
            Assert.Single(graph.Nodes);
        }

        [Fact]
        public void Resolve_RequirementNotProvided_ReportsProblem()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude> { new() { Ref = "cards", Role = "battle" } },
            };
            var cards = new ModCollection { Id = "cards", Name = "Cards", Requires = new List<string> { "character_system" } };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, cards), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            var problem = Assert.Single(graph.Problems);
            Assert.Equal(ModCollectionProblemKind.RequirementMissing, problem.Kind);
            Assert.Equal("character_system", problem.Title.Split('\'')[3]);
            Assert.Equal("cards", problem.CollectionId);
        }

        [Fact]
        public void Resolve_RequirementProvidedBySibling_NoProblem()
        {
            var game = new ModCollection
            {
                Id = "game",
                Includes = new List<ModCollectionInclude>
                {
                    new() { Ref = "cards", Role = "battle" },
                    new() { Ref = "rpg_2d", Role = "systems" },
                },
            };
            var cards = new ModCollection { Id = "cards", Requires = new List<string> { "character_system" } };
            var rpg = new ModCollection { Id = "rpg_2d", Provides = new List<string> { "character_system" } };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, cards, rpg), Mods(), new HashSet<string>(), new List<CatalogExportEntry>());

            Assert.Empty(graph.Problems);
        }

        private static Dictionary<string, ModCollection> Collections(params ModCollection[] items)
        {
            var result = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in items)
                result[collection.Id] = collection;
            return result;
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
