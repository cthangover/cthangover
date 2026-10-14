using Cthangover.Core.Actions;
using Cthangover.Core.Factories.Impls;

namespace Engine.Tests.Actions
{
    public class ScenarioActionFactoryTests
    {
        [Fact]
        public void Factory_Singleton_ReturnsSameInstance()
        {
            var first = ScenarioActionFactory.Instance;
            var second = ScenarioActionFactory.Instance;

            Assert.Same(first, second);
        }

        [Fact]
        public void Factory_GetAll_ReturnsActions()
        {
            var all = ScenarioActionFactory.Instance.GetAll();

            Assert.NotEmpty(all);
            foreach (var action in all)
            {
                Assert.False(string.IsNullOrEmpty(action.Name));
            }
        }

        [Fact]
        public void Factory_Get_ReturnsActionByName()
        {
            var action = ScenarioActionFactory.Instance.Get("scene.instantiate");

            Assert.NotNull(action);
            Assert.Equal("scene.instantiate", action.Name);
        }

        [Fact]
        public void Factory_Get_ReturnsCorrectType()
        {
            var action = ScenarioActionFactory.Instance.Get("scene.instantiate");
            Assert.IsAssignableFrom<IScenarioAction>(action);
        }

        [Fact]
        public void Factory_Get_ReturnsNullOnUnknownName()
        {
            var result = ScenarioActionFactory.Instance.Get("nonexistent.action");
            Assert.Null(result);
        }

        // quest.* actions (set_status, set_data_status, add_tag, remove_tag,
        // send_notification) live in the quests_base engine mod and are
        // discovered at mod load; they are covered by the integration
        // coverage_quests test, not by core-only unit tests.

        [Fact]
        public void Factory_DiscoversSceneActions()
        {
            var all = ScenarioActionFactory.Instance.GetAll();
            var sceneActions = all.Where(a =>
                a.Name.StartsWith("scene."));

            Assert.Contains(sceneActions, a => a.Name == "scene.instantiate");
            Assert.Contains(sceneActions, a => a.Name == "scene.remove_object");
        }

        [Fact]
        public void Factory_DiscoversCharacterActions()
        {
            var all = ScenarioActionFactory.Instance.GetAll();
            var charActions = all.Where(a =>
                a.Name.StartsWith("character."));

            Assert.Contains(charActions, a => a.Name == "character.add_to_party");
            Assert.Contains(charActions, a => a.Name == "character.send_notification");
            Assert.Contains(charActions, a => a.Name == "character.add_skill");
        }

    }
}
