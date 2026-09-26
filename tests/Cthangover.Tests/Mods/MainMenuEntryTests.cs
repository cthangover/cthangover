using System.Text.Json;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Contract tests for the <c>main_menu</c> manifest DTO. The registry and
    /// builder are exercised through runtime integration scenes; here only the
    /// serialization contract is locked down, because a silently renamed JSON
    /// property would make every mod's menu button disappear without an error.
    /// </summary>
    public class MainMenuEntryTests
    {
        [Fact]
        public void JsonRoundTrip_PreservesSerializedFields()
        {
            var original = new MainMenuEntry
            {
                Id = "mod_manager",
                ScenePath = "scenes/mod_manager_screen.tscn",
                Button = new UiButtonEntry
                {
                    Icon = "mod_manager",
                    Locale = "mod_manager/menu_title",
                    Index = 100,
                },
            };

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<MainMenuEntry>(json);

            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.ScenePath, restored.ScenePath);
            Assert.NotNull(restored.Button);
            Assert.Equal(original.Button.Icon, restored.Button.Icon);
            Assert.Equal(original.Button.Locale, restored.Button.Locale);
            Assert.Equal(original.Button.Index, restored.Button.Index);
        }

        [Fact]
        public void JsonRoundTrip_RuntimeFieldsAreNotSerialized()
        {
            var original = new MainMenuEntry
            {
                Id = "x",
                ScenePath = "scenes/x.tscn",
                Button = new UiButtonEntry { Locale = "x/title" },
                ModId = "some_mod",
                ModOrderIndex = 7,
            };

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<MainMenuEntry>(json);

            Assert.Null(restored.ModId);
            Assert.Equal(0, restored.ModOrderIndex);
        }

        [Fact]
        public void JsonDeserialize_UsesSnakeCaseNames()
        {
            const string json = """
            {
              "id": "mods",
              "scene_path": "scenes/mods.tscn",
              "button": { "icon": "book", "locale": "menu/mods", "index": 30 }
            }
            """;

            var entry = JsonSerializer.Deserialize<MainMenuEntry>(json);

            Assert.Equal("mods", entry.Id);
            Assert.Equal("scenes/mods.tscn", entry.ScenePath);
            Assert.Equal("book", entry.Button.Icon);
            Assert.Equal("menu/mods", entry.Button.Locale);
            Assert.Equal(30, entry.Button.Index);
        }

        [Fact]
        public void ButtonIndex_DefaultsToTen()
        {
            Assert.Equal(10, new UiButtonEntry().Index);
        }
    }
}
