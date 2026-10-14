using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Round-trip tests for the scheme layout document. Layouts are pure UI
    /// state persisted to <c>user://collection_layout.json</c>: group frames and
    /// chip positions must survive serialization exactly, because a lost
    /// coordinate silently resets the player's arrangement on the next launch.
    /// </summary>
    public class ModCollectionLayoutTests
    {
        [Fact]
        public void SerializeDeserialize_RoundTripsGroupsAndMods()
        {
            var original = new ModCollectionLayout
            {
                Groups = new Dictionary<string, ModCollectionLayoutRect>
                {
                    ["i:rpg_novella:rpg_2d"] = new() { X = 10f, Y = 20f, W = 458f, H = 390f },
                },
                Mods = new Dictionary<string, ModCollectionLayoutPoint>
                {
                    ["i:rpg_novella:rpg_2d|base_2d"] = new() { X = 14f, Y = 76f },
                },
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions);
            var restored = JsonSerializer.Deserialize<ModCollectionLayout>(json, JsonOptionsProvider.ReadOptions);

            var rect = restored.Groups["i:rpg_novella:rpg_2d"];
            Assert.Equal(10f, rect.X);
            Assert.Equal(20f, rect.Y);
            Assert.Equal(458f, rect.W);
            Assert.Equal(390f, rect.H);
            var point = restored.Mods["i:rpg_novella:rpg_2d|base_2d"];
            Assert.Equal(14f, point.X);
            Assert.Equal(76f, point.Y);
        }

        [Fact]
        public void Deserialize_EmptyObject_ProducesEmptyDictionaries()
        {
            const string json = "{}";

            var layout = JsonSerializer.Deserialize<ModCollectionLayout>(json, JsonOptionsProvider.ReadOptions);

            Assert.NotNull(layout.Groups);
            Assert.NotNull(layout.Mods);
            Assert.Empty(layout.Groups);
            Assert.Empty(layout.Mods);
        }
    }
}
