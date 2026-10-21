using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Games
{
    /// <summary>
    /// A game boots with the mods its current collection resolves to, plus the
    /// front-end and explicit extras — computed from the definition on every
    /// launch, so a changed mechanic changes the next run.
    /// </summary>
    public class GameModSetTests
    {
        [Fact]
        public void Compute_TakesGameGraphAndFrontend()
        {
            var mods = Mods(Local("story"), Local("engine"), Local("cards", "engine"), Local("menu"), Local("unrelated"));
            var collections = Collections(
                Game("novel", Mechanic("core", "story"), Mechanic("battle", "cards")),
                Frontend("frontend", Mechanic("menu", "menu")));

            var set = GameModSet.Compute("novel", collections, mods, Array.Empty<string>());

            Assert.Equal(new HashSet<string> { "story", "cards", "engine", "menu" }, set.ModIds);
            Assert.Empty(set.MissingMods);
        }

        [Fact]
        public void Compute_FollowsEditedMechanics()
        {
            var mods = Mods(Local("story"), Local("cards"), Local("ff"));
            var game = Game("novel", Mechanic("core", "story"), Mechanic("battle", "cards"));
            var collections = Collections(game);
            Assert.Contains("cards", GameModSet.Compute("novel", collections, mods, Array.Empty<string>()).ModIds);

            game.Mechanics[1].Mods = new List<string> { "ff" };
            var edited = GameModSet.Compute("novel", collections, mods, Array.Empty<string>());

            Assert.Contains("ff", edited.ModIds);
            Assert.DoesNotContain("cards", edited.ModIds);
        }

        [Fact]
        public void Compute_SkipsOptionalMechanicTurnedOff()
        {
            var mods = Mods(Local("story"), Local("cards"));
            var battle = Mechanic("battle", "cards");
            battle.Optional = true;
            battle.Selected = false;

            var set = GameModSet.Compute("novel", Collections(Game("novel", Mechanic("core", "story"), battle)), mods, Array.Empty<string>());

            Assert.DoesNotContain("cards", set.ModIds);
        }

        [Fact]
        public void Compute_ReportsMissingAndAddsExtrasWithDependencies()
        {
            var mods = Mods(Local("story"), Local("runner", "dialogs"), Local("dialogs"));

            var set = GameModSet.Compute("novel", Collections(Game("novel", Mechanic("core", "story", "lost"))), mods,
                Array.Empty<string>(), new[] { "runner" });

            Assert.Contains("lost", set.MissingMods);
            Assert.Contains("runner", set.ModIds);
            Assert.Contains("dialogs", set.ModIds);
        }

        [Fact]
        public void Compute_OptionalDependencyNotInstalled_IsNotMissing()
        {
            var mods = Mods(Local("story", "voice?"), Local("extras", "lore>=1?"), Local("engine"));
            var collections = Collections(Game("novel", Mechanic("core", "story", "engine")));

            var set = GameModSet.Compute("novel", collections, mods, Array.Empty<string>(), new[] { "extras" });

            Assert.Equal(new HashSet<string> { "story", "engine", "extras" }, set.ModIds);
            Assert.Empty(set.MissingMods);
        }

        [Fact]
        public void Compute_UnknownOrNonGameCollection_ReturnsNull()
        {
            var collections = Collections(Frontend("frontend", Mechanic("menu", "menu")));

            Assert.Null(GameModSet.Compute("frontend", collections, Mods(), Array.Empty<string>()));
            Assert.Null(GameModSet.Compute("absent", collections, Mods(), Array.Empty<string>()));
        }

        [Fact]
        public void Collection_DeserializesEntrySettingsCoverAndFrontendKind()
        {
            const string json = @"{ ""id"": ""g"", ""kind"": ""frontend"", ""entry"": { ""scene"": ""intro"" },
                ""settings"": { ""battle.core"": ""ff_battle"" }, ""cover"": ""collections/g.png"" }";

            var collection = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal(ModCollectionKind.Frontend, collection.Kind);
            Assert.Equal("intro", collection.Entry.Scene);
            Assert.Equal("ff_battle", collection.Settings["battle.core"]);
            Assert.Equal("collections/g.png", collection.Cover);
        }

        private static ModCollection Game(string id, params ModCollectionMechanic[] mechanics)
            => new() { Id = id, Kind = ModCollectionKind.Game, Mechanics = new List<ModCollectionMechanic>(mechanics) };

        private static ModCollection Frontend(string id, params ModCollectionMechanic[] mechanics)
            => new() { Id = id, Kind = ModCollectionKind.Frontend, Mechanics = new List<ModCollectionMechanic>(mechanics) };

        private static ModCollectionMechanic Mechanic(string id, params string[] mods)
            => new() { Id = id, Mods = new List<string>(mods) };

        private static Dictionary<string, ModCollection> Collections(params ModCollection[] items)
        {
            var result = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
                result[item.Id] = item;
            return result;
        }

        private static ModInfo Local(string id, params string[] depends)
        {
            var manifest = new ModManifest { Id = id, Name = id, Version = "1.0.0", Depends = new List<ModDependency>() };
            foreach (var dependency in depends)
                manifest.Depends.Add(ModDependency.Parse(dependency));
            return new ModInfo { Id = id, Name = id, Manifest = manifest };
        }

        private static Dictionary<string, IModInfo> Mods(params ModInfo[] mods)
        {
            var result = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                result[mod.Id] = mod;
            return result;
        }
    }
}
