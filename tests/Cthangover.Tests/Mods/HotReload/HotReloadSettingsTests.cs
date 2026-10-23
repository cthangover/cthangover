namespace Cthangover.Tests.Mods.HotReload
{
    using Cthangover.Core.Mods.HotReload;

    /// <summary>When the mod watcher runs and which file changes trigger a reload.</summary>
    public class HotReloadSettingsTests
    {
        [Fact]
        public void IsEnabled_FromEditor_ByDefault()
        {
            Assert.True(HotReloadSettings.IsEnabled(runFromEditor: true, new string[0]));
            Assert.True(HotReloadSettings.IsEnabled(runFromEditor: true, new[] { "--game=cthangover", "--new" }));
        }

        [Fact]
        public void IsEnabled_ExportedBuild_OnlyWithTheFlag()
        {
            Assert.False(HotReloadSettings.IsEnabled(runFromEditor: false, null));
            Assert.True(HotReloadSettings.IsEnabled(runFromEditor: false, new[] { HotReloadSettings.EnableFlag }));
        }

        [Theory]
        [InlineData("--itest=new_game")]
        [InlineData("--run-tests")]
        [InlineData("--timeout=30")]
        [InlineData("--choices=0,1")]
        [InlineData("--resource-atlas")]
        [InlineData("--locale-atlas")]
        public void IsEnabled_AutomatedRuns_OffEvenFromEditor(string arg)
        {
            Assert.False(HotReloadSettings.IsEnabled(runFromEditor: true, new[] { arg }));
            Assert.True(HotReloadSettings.IsEnabled(runFromEditor: true, new[] { arg, HotReloadSettings.EnableFlag }));
        }

        [Fact]
        public void IsEnabled_DisableFlag_WinsOverEverything()
        {
            Assert.False(HotReloadSettings.IsEnabled(runFromEditor: true, new[] { HotReloadSettings.DisableFlag, HotReloadSettings.EnableFlag }));
        }

        [Theory]
        [InlineData("/game/mods/cooking/src/Cooking.cs")]
        [InlineData(@"C:\game\mods\cooking\manifest.json")]
        [InlineData("/game/mods/cthangover_game/scenarios/start.scenario")]
        [InlineData("/game/mods/menu_base/locale/ru.properties")]
        [InlineData("/game/mods/editor_tools/scenes/scenario_info_panel.tscn")]
        public void IsRelevantChange_ModContent(string path)
        {
            Assert.True(HotReloadSettings.IsRelevantChange(path));
        }

        [Theory]
        [InlineData("/game/mods/cooking/src/Cooking.cs.uid")]
        [InlineData("/game/mods/cooking/icon.png.import")]
        [InlineData("/game/mods/cooking/src/.Cooking.cs.swp")]
        [InlineData("/game/mods/cooking/src/Cooking.cs~")]
        [InlineData("/game/mods/cooking/.git/index")]
        [InlineData("/game/mods/live2d_base/source_code/obj/Live2D.dll")]
        [InlineData("/game/mods/x/save.tmp")]
        [InlineData("")]
        [InlineData(null)]
        public void IsRelevantChange_EditorAndBuildNoise(string path)
        {
            Assert.False(HotReloadSettings.IsRelevantChange(path));
        }
    }
}
