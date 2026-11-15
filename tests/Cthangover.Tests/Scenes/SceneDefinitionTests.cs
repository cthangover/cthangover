namespace Engine.Tests.Scenes
{
    using System.Text.Json;
    using Cthangover.Core.Scenes;
    using Xunit;
    public class SceneDefinitionTests
    {
        [Fact]
        public void ShellAndEventlessSavePermissionRoundTrip()
        {
            var original = new SceneDefinition { Name = "room", ModId = "sample", Shell = "volume", SaveAllowed = true, DefaultAmbient = "wind", DefaultScenario = "intro", DefaultBackground = new() { "sky" } };
            var restored = JsonSerializer.Deserialize<SceneDefinition>(JsonSerializer.Serialize(original));
            Assert.Equal(original.Name, restored.Name); Assert.Equal(original.ModId, restored.ModId);
            Assert.Equal(original.Shell, restored.Shell); Assert.Equal(original.SaveAllowed, restored.SaveAllowed);
            Assert.Equal(original.DefaultAmbient, restored.DefaultAmbient); Assert.Equal(original.DefaultScenario, restored.DefaultScenario); Assert.Equal(original.DefaultBackground, restored.DefaultBackground);
        }
        [Fact]
        public void OldScenesKeepImplicitShellAndScenarioSavePolicy()
        {
            var restored = JsonSerializer.Deserialize<SceneDefinition>("{\"Name\":\"town\"}");
            Assert.Null(restored.Shell); Assert.Null(restored.SaveAllowed);
        }
    }
}
