using Cthangover.Core.Scenes.Patching;

namespace Engine.Tests.Scenes
{
    public class ScenePatchTargetResolverTests
    {
        [Fact]
        public void Normalize_StripsSchemeAndLeadingSlashes()
        {
            Assert.Equal("scenes/menu/main_menu.tscn", ScenePatchTargetResolver.Normalize("res://scenes/menu/main_menu.tscn"));
            Assert.Equal("scenes/menu/main_menu.tscn", ScenePatchTargetResolver.Normalize("/scenes/menu/main_menu.tscn"));
        }

        [Fact]
        public void Normalize_StripsModsPrefix()
        {
            Assert.Equal("scenes/menu/main_menu.tscn",
                ScenePatchTargetResolver.Normalize("res://mods/my_mod/scenes/menu/main_menu.tscn"));
            Assert.Equal("scenes/ui/panel.tscn",
                ScenePatchTargetResolver.Normalize("mods/other_mod/scenes/ui/panel.tscn"));
        }

        [Fact]
        public void Normalize_ConvertsBackslashesAndEmptyInput()
        {
            Assert.Equal("scenes/menu/main_menu.tscn",
                ScenePatchTargetResolver.Normalize(@"res://mods\my_mod\scenes\menu\main_menu.tscn"));
            Assert.Equal("", ScenePatchTargetResolver.Normalize(null));
        }

        [Fact]
        public void MatchesSceneKey_ExactAndSuffix()
        {
            Assert.True(ScenePatchTargetResolver.MatchesSceneKey("scenes/menu/main_menu.tscn", "res://scenes/menu/main_menu.tscn"));
            Assert.True(ScenePatchTargetResolver.MatchesSceneKey("scenes/menu/main_menu.tscn", "res://mods/my_mod/scenes/menu/main_menu.tscn"));
            Assert.False(ScenePatchTargetResolver.MatchesSceneKey("scenes/menu/main_menu.tscn", "res://scenes/menu/settings_menu.tscn"));
        }

        [Fact]
        public void Resolve_PrefersExactKeyOverSuffix()
        {
            var keys = new[] { "menu/main_menu.tscn", "scenes/menu/main_menu.tscn" };

            var result = ScenePatchTargetResolver.Resolve("res://scenes/menu/main_menu.tscn", keys);

            Assert.Equal("scenes/menu/main_menu.tscn", result);
        }

        [Fact]
        public void Resolve_SuffixMatchWhenKeyHasExtraPrefix()
        {
            var keys = new[] { "scenes/menu/main_menu.tscn" };

            var result = ScenePatchTargetResolver.Resolve("res://mods/my_mod/scenes/menu/main_menu.tscn", keys);

            Assert.Equal("scenes/menu/main_menu.tscn", result);
            Assert.Null(ScenePatchTargetResolver.Resolve("res://scenes/menu/missing.tscn", keys));
        }
    }
}
