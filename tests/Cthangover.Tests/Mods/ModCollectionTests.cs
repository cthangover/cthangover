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
                        Mods = new List<string> { "visual_novel_core>=1" },
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
            Assert.Equal("visual_novel_core>=1", restored.Mechanics[0].Mods[0]);
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
    }
}
