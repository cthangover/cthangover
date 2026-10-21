using System.Collections.Generic;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// The scenario replacement rule: definitions are collected flat in load order and
    /// a later definition of the same name replaces the earlier one — wherever
    /// either of them lives — while different names live side by side.
    /// </summary>
    public class ScenarioReplacementTests
    {
        private static ScenarioDefinition Def(string name, string scene, string mod) =>
            new() { Name = name, Scene = scene, ModId = mod, FilePath = $"scenarios/{name}.scenario" };


        [Fact]
        public void LaterModReplacesSameName()
        {
            var collected = new List<ScenarioDefinition>();
            collected.Add(Def("town_default", "town", "town_pack"));
            collected.Add(Def("town_default", "town", "my_novel_game"));

            var defs = ScenarioResolver.GroupReplacing(collected);

            var only = Assert.Single(defs["town"]);
            Assert.Equal("my_novel_game", only.ModId);
        }

        [Fact]
        public void DifferentNamesStayTogether()
        {
            var collected = new List<ScenarioDefinition>();
            collected.Add(Def("town_default", "town", "town_pack"));
            collected.Add(Def("my_novel_town_shop", "town", "my_novel_game"));

            var defs = ScenarioResolver.GroupReplacing(collected);

            Assert.Equal(2, defs["town"].Count);
        }

        [Fact]
        public void ReplacementMayMoveTheScenarioToAnotherScene()
        {
            var collected = new List<ScenarioDefinition>();
            collected.Add(Def("intro", "town", "town_pack"));
            collected.Add(Def("intro", "home", "my_novel_game"));

            var defs = ScenarioResolver.GroupReplacing(collected);

            Assert.False(defs.ContainsKey("town"));
            Assert.Equal("my_novel_game", Assert.Single(defs["home"]).ModId);
        }

        [Fact]
        public void ReplacementMovedIntoAnEarlierSceneStillWins()
        {
            var collected = new List<ScenarioDefinition>();
            collected.Add(Def("shop", "home", "town_pack"));
            collected.Add(Def("intro", "town", "town_pack"));
            collected.Add(Def("intro", "home", "my_novel_game"));

            var defs = ScenarioResolver.GroupReplacing(collected);

            Assert.False(defs.ContainsKey("town"));
            Assert.Equal(new[] { "town_pack", "my_novel_game" }, defs["home"].ConvertAll(d => d.ModId));
        }
    }
}
