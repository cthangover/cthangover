namespace Engine.Tests.Scenarios
{
    using Cthangover.Core.Scenarios;

    /// <summary>Map-event scripts may begin with commands containing colons, not only scene front matter.</summary>
    public class ScenarioMetadataTests
    {
        [Theory]
        [InlineData("when \"wolf_dead == 1\" -> :reward\nend")]
        [InlineData("check cha 12 -> :success\nend")]
        [InlineData("ptext \"Debt: thirty years.\"\nend")]
        [InlineData("# A note: this script is headerless\nwhen \"ready == 1\" -> :done\nend")]
        [InlineData(":reward\naction item.add id=money/coin\nend")]
        public void HeaderlessCommandsAreRetained(string script)
        {
            var result = ScenarioMetadata.ParseMetadata(script);
            Assert.Empty(result.metadata);
            Assert.Equal(script, result.remainingText);
        }

        [Fact]
        public void SceneHeaderAndAuthorKeysRemainSupported()
        {
            const string script = "scene: rpg_village\npriority: 15\ncondition: item('game/lantern') >= 1\ncustom-key: value\n---\nwhen \"ready == 1\" -> :done\nend";
            var result = ScenarioMetadata.ParseMetadata(script);
            Assert.Equal("rpg_village", result.metadata["scene"]);
            Assert.Equal("15", result.metadata["priority"]);
            Assert.Equal("item('game/lantern') >= 1", result.metadata["condition"]);
            Assert.Equal("value", result.metadata["custom-key"]);
            Assert.Equal("when \"ready == 1\" -> :done\nend", result.remainingText);
        }

        [Fact]
        public void HeaderWithoutSeparatorStopsAtFirstCommand()
        {
            var result = ScenarioMetadata.ParseMetadata("scene: room\nwhen \"open == 1\" -> :inside\nend");
            Assert.Single(result.metadata);
            Assert.Equal("room", result.metadata["scene"]);
            Assert.Equal("when \"open == 1\" -> :inside\nend", result.remainingText);
        }
    }
}
