using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the '@key' localization convention at the resolver level:
    /// '@'-prefixed names stay raw without a translator, resolve through an
    /// injected translator, degrade gracefully when the translation is
    /// missing, and respect the locale[] / flat-field priority order. The
    /// translator is a test double — no Godot is involved.
    /// </summary>
    public class ModCollectionLocalizationTests
    {
        private static readonly Func<string, string> Translator =
            raw => raw != null && raw.StartsWith("@") ? "T:" + raw.Substring(1) : raw;

        [Fact]
        public void Resolve_WithoutTranslator_MechanicKeyStaysRaw()
        {
            var collection = Collection(Mechanic("core", "@collections/game/core", "@collections/game/core_desc"));

            var graph = ModCollectionResolver.Resolve(collection, Mods(), Disabled(), Catalog());

            var group = Assert.Single(graph.Groups);
            Assert.Equal("@collections/game/core", group.Name);
            Assert.Equal("@collections/game/core_desc", group.Description);
        }

        [Fact]
        public void Resolve_WithTranslator_MechanicKeyResolved()
        {
            var collection = Collection(Mechanic("core", "@collections/game/core", "@collections/game/core_desc"));

            var graph = ModCollectionResolver.Resolve(
                collection, Mods(), Disabled(), Catalog(), Session(), Translator);

            var group = Assert.Single(graph.Groups);
            Assert.Equal("T:collections/game/core", group.Name);
            Assert.Equal("T:collections/game/core_desc", group.Description);
        }

        [Fact]
        public void Resolve_TranslatorMissingTranslation_KeepsRawValueGracefully()
        {
            var collection = Collection(Mechanic("core", "@collections/game/core"));

            Func<string, string> missing = _ => null;
            var graph = ModCollectionResolver.Resolve(
                collection, Mods(), Disabled(), Catalog(), Session(), missing);

            var group = Assert.Single(graph.Groups);
            // A missing translation must never blank out the group name.
            Assert.Equal("@collections/game/core", group.Name);
        }

        [Fact]
        public void Resolve_ExactLocaleEntry_BeatsFlatKey()
        {
            var mechanic = new ModCollectionMechanic
            {
                Id = "core",
                Name = "@collections/game/core",
                Locale = new List<CatalogLocaleEntry> { new() { Lang = "ru", Name = "Русское ядро" } },
                Mods = new List<string> { "app" },
            };
            var collection = Collection(mechanic);

            var graph = ModCollectionResolver.Resolve(
                collection, null, Mods(), Disabled(), Catalog(), Session(), "ru", Translator);

            var group = Assert.Single(graph.Groups);
            Assert.Equal("Русское ядро", group.Name);
        }

        [Fact]
        public void Resolve_FlatKey_BeatsEnglishLocaleEntry()
        {
            var mechanic = new ModCollectionMechanic
            {
                Id = "core",
                Name = "@collections/game/core",
                Locale = new List<CatalogLocaleEntry> { new() { Lang = "en", Name = "English" } },
                Mods = new List<string> { "app" },
            };
            var collection = Collection(mechanic);

            var graph = ModCollectionResolver.Resolve(
                collection, null, Mods(), Disabled(), Catalog(), Session(), "ru", Translator);

            var group = Assert.Single(graph.Groups);
            Assert.Equal("T:collections/game/core", group.Name);
        }

        [Fact]
        public void Resolve_IncludeLabelNoteKeys_AndChildNameKey_Resolved()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude>
                {
                    new() { Ref = "cards", Role = "battle", Label = "@labels/cards", Note = "@labels/cards_note" },
                    new() { Ref = "story", Role = "story" },
                },
            };
            var cards = new ModCollection
            {
                Id = "cards",
                Name = "@collections/cards/name",
                Description = "@collections/cards/description",
            };
            var story = new ModCollection { Id = "story", Name = "Story" };
            var store = Collections(game, cards, story);

            var graph = ModCollectionResolver.Resolve(
                game, store, Mods(), Disabled(), Catalog(), Session(), null, Translator);

            Assert.Equal(2, graph.Groups.Count);
            var labelled = graph.Groups.First(g => g.CollectionId == "cards");
            Assert.Equal("T:labels/cards", labelled.Name);
            Assert.Equal("T:labels/cards_note", labelled.Description);

            // Without label/note the child collection's own plain name passes through.
            var plain = graph.Groups.First(g => g.CollectionId == "story");
            Assert.Equal("Story", plain.Name);
        }

        [Fact]
        public void Resolve_ChildCollectionNameKey_ResolvedThroughTranslator()
        {
            var game = new ModCollection
            {
                Id = "game",
                Name = "Game",
                Includes = new List<ModCollectionInclude> { new() { Ref = "cards", Role = "battle" } },
            };
            var cards = new ModCollection
            {
                Id = "cards",
                Name = "@collections/cards/name",
                Description = "@collections/cards/description",
            };

            var graph = ModCollectionResolver.Resolve(
                game, Collections(game, cards), Mods(), Disabled(), Catalog(), Session(), null, Translator);

            var group = Assert.Single(graph.Groups);
            Assert.Equal("T:collections/cards/name", group.Name);
            Assert.Equal("T:collections/cards/description", group.Description);
        }

        [Fact]
        public void Resolve_PlainNames_PassThroughTranslatorUnchanged()
        {
            var collection = Collection(Mechanic("core", "Core", "Plain description"));

            var graph = ModCollectionResolver.Resolve(
                collection, Mods(), Disabled(), Catalog(), Session(), Translator);

            var group = Assert.Single(graph.Groups);
            Assert.Equal("Core", group.Name);
            Assert.Equal("Plain description", group.Description);
        }

        private static ModCollection Collection(params ModCollectionMechanic[] mechanics)
            => new()
            {
                Id = "test",
                Name = "Test",
                Mechanics = new List<ModCollectionMechanic>(mechanics),
            };

        private static ModCollectionMechanic Mechanic(string id, string name, string description = null)
            => new() { Id = id, Name = name, Description = description, Mods = new List<string> { "app" } };

        private static Dictionary<string, ModCollection> Collections(params ModCollection[] items)
        {
            var result = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in items)
                result[collection.Id] = collection;
            return result;
        }

        private static Dictionary<string, IModInfo> Mods() => new(StringComparer.OrdinalIgnoreCase);

        private static HashSet<string> Disabled() => new();

        private static List<CatalogExportEntry> Catalog() => new();

        private static HashSet<string> Session() => new();
    }
}
