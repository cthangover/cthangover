using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the game gallery aggregation: only Game collections are
    /// summarized, readiness reflects node states and graph problems, the
    /// plan counts mirror <see cref="ModCollectionPlan"/>, and the mod-side
    /// cross links find every game that references a mod (directly or through
    /// includes). No Godot is involved.
    /// </summary>
    public class ModCollectionGalleryTests
    {
        [Fact]
        public void Build_NullCollections_ReturnsEmptyList()
        {
            var result = ModCollectionGallery.Build(
                null, Mods(), Disabled(), Catalog(), Session());

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Build_EverythingInstalled_SummaryIsReady()
        {
            var store = Collections(Game("ready", Mechanic("core", "app")));
            var mods = Mods(Local("app", "1.0"));

            var result = ModCollectionGallery.Build(store, mods, Disabled(), Catalog(), Session());

            var summary = Assert.Single(result);
            Assert.Equal("ready", summary.Collection.Id);
            Assert.Equal(1, summary.TotalMods);
            Assert.Equal(1, summary.InstalledCount);
            Assert.Contains("app", summary.ReadyMods);
            Assert.Empty(summary.MissingIds);
            Assert.Empty(summary.MismatchIds);
            Assert.Equal(0, summary.ProblemsCount);
            Assert.Equal(0, summary.PlanInstallCount);
            Assert.Equal(0, summary.PlanEnableCount);
            Assert.True(summary.IsReady);
        }

        [Fact]
        public void Build_Language_PicksTheCollectionsOwnLocaleEntry()
        {
            var game = Game("vn", Mechanic("core", "app"));
            game.Description = "A novel";
            game.Locale = new List<CatalogLocaleEntry>
            {
                new() { Lang = "ru", Name = "Новелла", Description = "Новелла о городе" },
            };
            var store = Collections(game);

            var english = Assert.Single(ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session()));
            var russian = Assert.Single(ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session(), null, "ru_RU"));

            Assert.Equal("A novel", english.Description);
            Assert.Equal("Новелла", russian.Name);
            Assert.Equal("Новелла о городе", russian.Description);
        }

        [Fact]
        public void Build_MissingAndCatalogMods_NotReadyWithInstallPlan()
        {
            var store = Collections(Game("broken", Mechanic("core", "gone", "remote")));
            var catalog = Catalog(CatalogEntry("remote", "1.0"));

            var result = ModCollectionGallery.Build(store, Mods(), Disabled(), catalog, Session());

            var summary = Assert.Single(result);
            Assert.Equal(2, summary.TotalMods);
            Assert.Equal(0, summary.InstalledCount);
            Assert.Contains("gone", summary.MissingIds);
            Assert.Empty(summary.MismatchIds);
            Assert.Equal(0, summary.ProblemsCount);
            Assert.Equal(1, summary.PlanInstallCount);
            Assert.False(summary.IsReady);
        }

        [Fact]
        public void Build_BrokenInclude_CountsGraphProblem()
        {
            var game = Game("problem", Mechanic("core", "app"));
            game.Includes = new List<ModCollectionInclude> { new() { Ref = "ghost_block", Role = "systems" } };
            var store = Collections(game);
            var mods = Mods(Local("app", "1.0"));

            var result = ModCollectionGallery.Build(store, mods, Disabled(), Catalog(), Session());

            var summary = Assert.Single(result);
            Assert.Contains("app", summary.ReadyMods);
            Assert.Equal(1, summary.ProblemsCount);
            Assert.False(summary.IsReady);
        }

        [Fact]
        public void Build_DisabledMod_GoesToEnablePlan()
        {
            var store = Collections(Game("off_game", Mechanic("core", "off")));
            var mods = Mods(Local("off", "1.0"));
            var disabled = new HashSet<string> { "off" };

            var result = ModCollectionGallery.Build(store, mods, disabled, Catalog(), Session());

            var summary = Assert.Single(result);
            Assert.Equal(0, summary.InstalledCount);
            Assert.Empty(summary.ReadyMods);
            Assert.Equal(1, summary.PlanEnableCount);
            // The specified IsReady formula counts only missing/mismatched
            // mods and graph problems — a merely disabled mod is an enable
            // click away, surfaced through PlanEnableCount, not IsReady.
            Assert.True(summary.IsReady);
        }

        [Fact]
        public void Build_VersionMismatch_NotReadyNoInstallPlanned()
        {
            var game = Game("pinned", Mechanic("core", "app>=2"));
            var store = Collections(game);
            var mods = Mods(Local("app", "1.0"));

            var result = ModCollectionGallery.Build(store, mods, Disabled(), Catalog(), Session());

            var summary = Assert.Single(result);
            Assert.Contains("app", summary.MismatchIds);
            Assert.Equal(0, summary.PlanInstallCount);
            Assert.False(summary.IsReady);
        }

        [Fact]
        public void Build_NonGameCollections_AreSkipped()
        {
            var game = Game("only_game", Mechanic("core", "app"));
            var component = new ModCollection
            {
                Id = "component",
                Kind = ModCollectionKind.Component,
                Mechanics = new List<ModCollectionMechanic> { Mechanic("core", "lib") },
            };
            var toolset = new ModCollection { Id = "toolset", Kind = ModCollectionKind.Toolset };
            var store = Collections(game, component, toolset);
            var mods = Mods(Local("app", "1.0"));

            var result = ModCollectionGallery.Build(store, mods, Disabled(), Catalog(), Session());

            var summary = Assert.Single(result);
            Assert.Equal("only_game", summary.Collection.Id);
        }

        [Fact]
        public void Build_OrderedByNameThenId()
        {
            var first = Game("b_game");
            first.Name = "Alpha";
            var second = Game("a_game");
            second.Name = "beta";
            var store = Collections(first, second);

            var result = ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session());

            Assert.Equal(2, result.Count);
            Assert.Equal("b_game", result[0].Collection.Id);
            Assert.Equal("a_game", result[1].Collection.Id);
        }

        [Fact]
        public void Build_TranslatorResolvesNameKeys_AndIsSkippedWhenAbsent()
        {
            var game = Game("keyed");
            game.Name = "@collections/keyed/name";
            game.Description = "@collections/keyed/description";
            var store = Collections(game);

            Func<string, string> translator = raw =>
                raw != null && raw.StartsWith("@") ? "T:" + raw.Substring(1) : raw;

            var with = ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session(), translator);
            Assert.Equal("T:collections/keyed/name", with[0].Name);
            Assert.Equal("T:collections/keyed/description", with[0].Description);

            var without = ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session());
            Assert.Equal("@collections/keyed/name", without[0].Name);
            Assert.Equal("@collections/keyed/description", without[0].Description);
        }

        [Fact]
        public void Build_OriginPassedThroughUnchanged()
        {
            var user = Game("user_game");
            user.Origin = ModCollectionOrigin.User;
            var builtIn = Game("builtin_game");
            builtIn.Origin = ModCollectionOrigin.BuiltIn;
            var catalog = Game("catalog_game");
            catalog.Origin = ModCollectionOrigin.Catalog;
            var store = Collections(user, builtIn, catalog);

            var result = ModCollectionGallery.Build(store, Mods(), Disabled(), Catalog(), Session());

            Assert.Equal(3, result.Count);
            Assert.Equal(ModCollectionOrigin.User, result.Single(s => s.Collection.Id == "user_game").Collection.Origin);
            Assert.Equal(ModCollectionOrigin.BuiltIn, result.Single(s => s.Collection.Id == "builtin_game").Collection.Origin);
            Assert.Equal(ModCollectionOrigin.Catalog, result.Single(s => s.Collection.Id == "catalog_game").Collection.Origin);
        }

        [Fact]
        public void FindGamesUsingMod_DirectMechanicReference()
        {
            var store = Collections(Game("adventure", Mechanic("core", "app>=1")));

            var map = ModCollectionGallery.FindGamesUsingMod(store, "app");

            Assert.Contains("app", map.Keys);
            Assert.Equal(new List<string> { "adventure" }, map["app"]);
        }

        [Fact]
        public void FindGamesUsingMod_VersionedQuery_MatchesByIdWithoutVersion()
        {
            var store = Collections(Game("adventure", Mechanic("core", "app>=1")));

            var map = ModCollectionGallery.FindGamesUsingMod(store, "app>=2");

            Assert.Equal(new List<string> { "adventure" }, map["app"]);
        }

        [Fact]
        public void FindGamesUsingMod_ThroughIncludes_FindsTransitiveGame()
        {
            var game = Game("adventure");
            game.Includes = new List<ModCollectionInclude> { new() { Ref = "cards_pack", Role = "battle" } };
            var pack = new ModCollection
            {
                Id = "cards_pack",
                Kind = ModCollectionKind.Component,
                Mechanics = new List<ModCollectionMechanic> { Mechanic("deck", "deck_core") },
            };
            var store = Collections(game, pack);

            var map = ModCollectionGallery.FindGamesUsingMod(store, "deck_core");

            Assert.Equal(new List<string> { "adventure" }, map["deck_core"]);
        }

        [Fact]
        public void FindGamesUsingMod_ComponentOnlyUsage_NotReported()
        {
            var loose = new ModCollection
            {
                Id = "loose",
                Kind = ModCollectionKind.Component,
                Mechanics = new List<ModCollectionMechanic> { Mechanic("core", "orphan") },
            };
            var store = Collections(loose);

            var map = ModCollectionGallery.FindGamesUsingMod(store, "orphan");

            Assert.Contains("orphan", map.Keys);
            Assert.Empty(map["orphan"]);
        }

        [Fact]
        public void FindGamesUsingMod_UnknownOrEmptyMod_YieldsEmptyResults()
        {
            var store = Collections(Game("adventure", Mechanic("core", "app")));

            Assert.Empty(ModCollectionGallery.FindGamesUsingMod(store, "unknown_mod")["unknown_mod"]);
            Assert.Empty(ModCollectionGallery.FindGamesUsingMod(store, string.Empty));
            Assert.Empty(ModCollectionGallery.FindGamesUsingMod(store, null));
            Assert.Empty(ModCollectionGallery.FindGamesUsingMod(null, "app"));
        }

        [Fact]
        public void FindGamesUsingMod_IncludeCycle_Terminates()
        {
            var a = Game("a_game");
            a.Includes = new List<ModCollectionInclude> { new() { Ref = "b_block", Role = "x" } };
            var b = new ModCollection
            {
                Id = "b_block",
                Kind = ModCollectionKind.Component,
                Mechanics = new List<ModCollectionMechanic> { Mechanic("x", "shared") },
                Includes = new List<ModCollectionInclude> { new() { Ref = "a_game", Role = "y" } },
            };
            var store = Collections(a, b);

            // The reference sits behind the include; the a<->b cycle must stop
            // the recursion instead of overflowing the stack.
            var map = ModCollectionGallery.FindGamesUsingMod(store, "shared");

            Assert.Equal(new List<string> { "a_game" }, map["shared"]);
        }

        private static ModCollection Game(string id, params ModCollectionMechanic[] mechanics)
            => new()
            {
                Id = id,
                Name = id,
                Mechanics = new List<ModCollectionMechanic>(mechanics),
            };

        private static ModCollectionMechanic Mechanic(string id, params string[] mods)
            => new() { Id = id, Name = id, Mods = new List<string>(mods) };

        private static Dictionary<string, ModCollection> Collections(params ModCollection[] items)
        {
            var result = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in items)
                result[collection.Id] = collection;
            return result;
        }

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

        private static HashSet<string> Disabled() => new();

        private static HashSet<string> Session() => new();

        private static List<CatalogExportEntry> Catalog() => new();

        private static List<CatalogExportEntry> Catalog(params CatalogExportEntry[] entries)
            => new(entries);

        private static CatalogExportEntry CatalogEntry(string id, params string[] versions)
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
