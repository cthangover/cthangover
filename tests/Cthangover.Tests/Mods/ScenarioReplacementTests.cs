using System.Collections.Generic;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// The scenario replacement rule: definitions are collected in load order and
    /// a later definition of the same name replaces the earlier one — wherever
    /// either of them lives — while different names live side by side.
    /// </summary>
    public class ScenarioReplacementTests
    {
        private static ScenarioDefinition Def(string name, string scene, string mod) =>
            new() { Name = name, Scene = scene, ModId = mod, FilePath = $"scenarios/{name}.scenario" };

        private static void Add(Dictionary<string, List<ScenarioDefinition>> defs, ScenarioDefinition def)
        {
            if (!defs.TryGetValue(def.Scene, out var list))
                defs[def.Scene] = list = new List<ScenarioDefinition>();
            list.Add(def);
        }

        [Fact]
        public void LaterModReplacesSameName()
        {
            var defs = new Dictionary<string, List<ScenarioDefinition>>();
            Add(defs, Def("town_default", "town", "town_pack"));
            Add(defs, Def("town_default", "town", "my_novel_game"));

            ScenarioResolver.DropReplaced(defs);

            var only = Assert.Single(defs["town"]);
            Assert.Equal("my_novel_game", only.ModId);
        }

        [Fact]
        public void DifferentNamesStayTogether()
        {
            var defs = new Dictionary<string, List<ScenarioDefinition>>();
            Add(defs, Def("town_default", "town", "town_pack"));
            Add(defs, Def("my_novel_town_shop", "town", "my_novel_game"));

            ScenarioResolver.DropReplaced(defs);

            Assert.Equal(2, defs["town"].Count);
        }

        [Fact]
        public void ReplacementMayMoveTheScenarioToAnotherScene()
        {
            var defs = new Dictionary<string, List<ScenarioDefinition>>();
            Add(defs, Def("intro", "town", "town_pack"));
            Add(defs, Def("intro", "home", "my_novel_game"));

            ScenarioResolver.DropReplaced(defs);

            Assert.False(defs.ContainsKey("town"));
            Assert.Equal("my_novel_game", Assert.Single(defs["home"]).ModId);
        }
    }
}
