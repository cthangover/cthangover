using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Round-trip tests for the collection JSON DTO. Collections are authored by
    /// hand, shipped inside mods and cached from catalogs, so every field must
    /// survive serialization exactly; <see cref="ModCollection.Origin"/> is
    /// runtime-only and must stay out of the file.
    /// </summary>
    public class ModCollectionTests
    {
        [Fact]
        public void SerializeDeserialize_FullCollection_RoundTripsAllFields()
        {
            var original = new ModCollection
            {
                Id = "card_novel",
                Name = "Card Novel",
                Author = "ct",
                Description = "A visual novel with card battles.",
                Version = "1.2.0",
                SourceUrls = new List<string> { "https://catalog.test/catalog.json" },
                Mechanics = new List<ModCollectionMechanic>
                {
                    new()
                    {
                        Id = "vn_core",
                        Name = "Novel Core",
                        Description = "Story engine.",
                        Mods = new List<string> { "cthangover_game>=1" },
                    },
                    new()
                    {
                        Id = "cards",
                        Name = "Card Game",
                        Mods = new List<string> { "card_battle>=1.1", "turn_battle_base>=1" },
                        Optional = true,
                    },
                },
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions);
            var restored = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Author, restored.Author);
            Assert.Equal(original.Description, restored.Description);
            Assert.Equal(original.Version, restored.Version);
            Assert.Equal(original.SourceUrls, restored.SourceUrls);
            Assert.Equal(2, restored.Mechanics.Count);

            Assert.Equal("vn_core", restored.Mechanics[0].Id);
            Assert.Equal("Novel Core", restored.Mechanics[0].Name);
            Assert.Equal("Story engine.", restored.Mechanics[0].Description);
            Assert.Single(restored.Mechanics[0].Mods);
            Assert.Equal("cthangover_game>=1", restored.Mechanics[0].Mods[0]);
            Assert.False(restored.Mechanics[0].Optional);

            Assert.Equal("cards", restored.Mechanics[1].Id);
            Assert.Equal(2, restored.Mechanics[1].Mods.Count);
            Assert.True(restored.Mechanics[1].Optional);
        }

        [Fact]
        public void Serialize_Origin_IsNotWrittenToJson()
        {
            var collection = new ModCollection { Id = "local", Origin = ModCollectionOrigin.User };

            var json = JsonSerializer.Serialize(collection, JsonOptionsProvider.WriteIndentedOptions);

            Assert.DoesNotContain("Origin", json);
        }

        [Fact]
        public void Deserialize_UnknownFields_AreIgnored()
        {
            const string json = """
            {
              "id": "x",
              "name": "X",
              "future_field": { "unknown": true },
              "mechanics": [ { "id": "m", "mods": ["a"], "another_future": 5 } ]
            }
            """;

            var collection = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal("x", collection.Id);
            Assert.Single(collection.Mechanics);
            Assert.Equal("a", collection.Mechanics[0].Mods[0]);
        }

        [Fact]
        public void DisplayTitle_FallsBackToId()
        {
            Assert.Equal("some_id", new ModCollection { Id = "some_id" }.DisplayTitle);
            Assert.Equal("Named", new ModCollection { Id = "x", Name = "Named" }.DisplayTitle);
        }

        [Fact]
        public void SerializeDeserialize_IncludesAndMetadata_RoundTripsAllFields()
        {
            var original = new ModCollection
            {
                Id = "rpg_novella",
                Kind = ModCollectionKind.Game,
                Name = "РПГ Новелла",
                Tags = new List<string> { "novel", "rpg" },
                Provides = new List<string> { "story_content" },
                Requires = new List<string> { "base_2d" },
                Includes = new List<ModCollectionInclude>
                {
                    new()
                    {
                        Ref = "rpg_2d>=1",
                        Role = "systems",
                        Optional = true,
                        Selected = false,
                        Label = "Основа",
                        Note = "Системы игры.",
                    },
                },
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions);
            var restored = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal(ModCollectionKind.Game, restored.Kind);
            Assert.Equal(original.Tags, restored.Tags);
            Assert.Equal(original.Provides, restored.Provides);
            Assert.Equal(original.Requires, restored.Requires);

            var include = Assert.Single(restored.Includes);
            Assert.Equal("rpg_2d>=1", include.Ref);
            Assert.Equal("systems", include.Role);
            Assert.True(include.Optional);
            Assert.False(include.Selected);
            Assert.Equal("Основа", include.Label);
            Assert.Equal("Системы игры.", include.Note);
            Assert.False(include.IsSelected);
        }

        [Fact]
        public void Deserialize_MissingKind_DefaultsToGame()
        {
            const string json = """{ "id": "legacy", "name": "Legacy", "mechanics": [] }""";

            var collection = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal(ModCollectionKind.Game, collection.Kind);
        }

        [Fact]
        public void Serialize_MechanicRoleAndSelected_RoundTrip()
        {
            var original = new ModCollection
            {
                Id = "x",
                Mechanics = new List<ModCollectionMechanic>
                {
                    new()
                    {
                        Id = "skins",
                        Name = "Темы",
                        Role = "ui_skin",
                        Optional = true,
                        Selected = false,
                        Mods = new List<string> { "ui_skin_dark>=1" },
                    },
                },
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions);
            var restored = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            var mechanic = Assert.Single(restored.Mechanics);
            Assert.Equal("ui_skin", mechanic.Role);
            Assert.True(mechanic.Optional);
            Assert.False(mechanic.Selected);
            Assert.False(mechanic.IsSelected);
        }

        [Fact]
        public void GetLocalizedName_PicksRequestedThenFlatThenEnglish()
        {
            var collection = new ModCollection
            {
                Id = "x",
                Name = "Base",
                Description = "Base description",
                Locale = new List<CatalogLocaleEntry>
                {
                    new() { Lang = "de", Name = "Basis", Description = "Deutsch" },
                    new() { Lang = "en", Name = "English", Description = "English text" },
                },
            };

            Assert.Equal("Basis", collection.GetLocalizedName("de"));
            Assert.Equal("Base", collection.GetLocalizedName("ru"));
            Assert.Equal("Base", collection.GetLocalizedName(null));
            Assert.Equal("Deutsch", collection.GetLocalizedDescription("de"));
            Assert.Equal("Base description", collection.GetLocalizedDescription("fr"));
        }

        [Theory]
        [InlineData("en-en")]
        [InlineData("en_US")]
        [InlineData("EN")]
        public void GetLocalizedName_RegionalOrCasedCode_MatchesTheLanguage(string language)
        {
            var collection = new ModCollection
            {
                Id = "x",
                Name = "Base",
                Locale = new List<CatalogLocaleEntry>
                {
                    new() { Lang = "ru", Name = "Основа", Description = "Русский" },
                    new() { Lang = "en", Name = "English", Description = "English text" },
                },
            };

            Assert.Equal("English", collection.GetLocalizedName(language));
            Assert.Equal("English text", collection.GetLocalizedDescription(language));
            Assert.Equal("Основа", collection.GetLocalizedName("ru-ru"));
        }

        [Fact]
        public void GetLocalizedName_NoFlatName_FallsBackToEnglishThenFirstThenId()
        {
            var collection = new ModCollection
            {
                Id = "x",
                Locale = new List<CatalogLocaleEntry>
                {
                    new() { Lang = "de", Name = "Basis", Description = "Deutsch" },
                    new() { Lang = "en", Name = "English", Description = "English text" },
                },
            };

            Assert.Equal("Basis", collection.GetLocalizedName("de"));
            Assert.Equal("English", collection.GetLocalizedName("ru"));
            Assert.Equal("English text", collection.GetLocalizedDescription("fr"));
            Assert.Equal("Basis", collection.GetLocalizedName(null));
            Assert.Equal("bare", new ModCollection { Id = "bare" }.GetLocalizedName("ru"));
        }

        [Fact]
        public void GetLocalizedName_FlatKey_ReturnedVerbatimForCallerTranslator()
        {
            var collection = new ModCollection
            {
                Id = "x",
                Name = "@collections/card_novel/name",
                Description = "@collections/card_novel/description",
            };

            Assert.Equal("@collections/card_novel/name", collection.GetLocalizedName("ru"));
            Assert.Equal("@collections/card_novel/name", collection.GetLocalizedName(null));
            Assert.Equal("@collections/card_novel/description", collection.GetLocalizedDescription("ru"));
        }

        [Fact]
        public void GetLocalizedName_NoLocale_FallsBackToFlatFields()
        {
            var collection = new ModCollection { Id = "x", Name = "Base", Description = "Flat" };

            Assert.Equal("Base", collection.GetLocalizedName("ru"));
            Assert.Equal("Flat", collection.GetLocalizedDescription("ru"));
        }

        [Fact]
        public void MechanicGetLocalizedName_FallsBackToNameThenId()
        {
            var mechanic = new ModCollectionMechanic
            {
                Id = "skins",
                Name = "Skins",
                Locale = new List<CatalogLocaleEntry>
                {
                    new() { Lang = "ru", Name = "Темы" },
                },
            };

            Assert.Equal("Темы", mechanic.GetLocalizedName("ru"));
            Assert.Equal("Skins", mechanic.GetLocalizedName("de"));

            var unnamed = new ModCollectionMechanic { Id = "core" };
            Assert.Equal("core", unnamed.GetLocalizedName("ru"));
        }

        [Fact]
        public void MechanicGetLocalizedName_FlatKeyBeatsEnglishLocale()
        {
            var mechanic = new ModCollectionMechanic
            {
                Id = "core",
                Name = "@collections/card_novel/battle",
                Locale = new List<CatalogLocaleEntry>
                {
                    new() { Lang = "en", Name = "English" },
                },
            };

            // Flat '@key' wins over the fixed English entry — the key resolves
            // to the player's current language through the translator.
            Assert.Equal("@collections/card_novel/battle", mechanic.GetLocalizedName("ru"));
            // The exact-language locale entry still beats the flat field.
            Assert.Equal("English", mechanic.GetLocalizedName("en"));
        }
    }
}
