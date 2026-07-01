using Cthangover.Core.Actions;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action.Impls;

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
            var action = ScenarioActionFactory.Instance.Get("quest.set_status");

            Assert.NotNull(action);
            Assert.Equal("quest.set_status", action.Name);
        }

        [Fact]
        public void Factory_Get_ReturnsCorrectType()
        {
            var action = ScenarioActionFactory.Instance.Get("quest.set_status");
            Assert.IsAssignableFrom<IScenarioAction>(action);
        }

        [Fact]
        public void Factory_Get_ReturnsNullOnUnknownName()
        {
            var result = ScenarioActionFactory.Instance.Get("nonexistent.action");
            Assert.Null(result);
        }

        [Fact]
        public void Factory_DiscoversQuestActions()
        {
            var all = ScenarioActionFactory.Instance.GetAll();
            var questActions = all.Where(a =>
                a.Name.StartsWith("quest."));

            Assert.Contains(questActions, a => a.Name == "quest.set_status");
            Assert.Contains(questActions, a => a.Name == "quest.set_data_status");
            Assert.Contains(questActions, a => a.Name == "quest.add_tag");
            Assert.Contains(questActions, a => a.Name == "quest.remove_tag");
            Assert.Contains(questActions, a => a.Name == "quest.send_notification");
        }

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
        }

        [Fact]
        public void ActionScenario_DoRun_ReturnsEarlyOnUnknownName()
        {
            var runtime = new DialogRuntime();
            var command = new ActionScenario { ActionName = "does.not.exist" };
            var exception = Record.Exception(() => command.DoRun(runtime));

            Assert.Null(exception);
        }

        [Fact]
        public void ActionScenario_DoRun_ReturnsEarlyOnEmptyName()
        {
            var runtime = new DialogRuntime();
            var command = new ActionScenario { ActionName = null };
            var exception = Record.Exception(() => command.DoRun(runtime));

            Assert.Null(exception);
        }
    }
}
