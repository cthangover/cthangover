using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    public class ResolveScenarioTests
    {
        private static string ScenarioFile(string name, string scene, string body)
        {
            return $"scene: {scene}\n---\n{body}";
        }

        [Fact]
        public void ResolveScenario_NullScenarioId_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");

            var result = ScenarioDefs.Resolve(null, provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_EmptyScenarioId_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");

            var result = ScenarioDefs.Resolve("", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_NotFound_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/other.scenario", ScenarioFile("other", "my_scene", "text hello"));

            var result = ScenarioDefs.Resolve("nonexistent", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_Found_ReturnsResolvedText()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/welcome.scenario", ScenarioFile("welcome", "main_scene", "text \"Hello\" key=greet"));

            var result = ScenarioDefs.Resolve("welcome", provider);

            Assert.NotNull(result);
            Assert.Contains("Hello", result);
        }

        [Fact]
        public void ResolveScenario_WithIncludes_ResolvesNested()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/dialog.scenario", ScenarioFile("dialog", "tavern", "text ${shared/greeting.txt}"));
            provider.AddFile("scenarios/shared/greeting.txt", "Hello, traveler!");

            var result = ScenarioDefs.Resolve("dialog", provider);

            Assert.NotNull(result);
            Assert.Contains("Hello, traveler!", result);
        }

        [Fact]
        public void ResolveScenario_BrokenInclude_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/broken.scenario", ScenarioFile("broken", "scene", "text ${missing}"));

            var result = ScenarioDefs.Resolve("broken", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_NoScenariosDirectory_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");

            var result = ScenarioDefs.Resolve("anything", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_InSubdirectory_FindsScenario()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/chapter1/start.scenario", ScenarioFile("start", "chapter1", "text begin"));

            var result = ScenarioDefs.Resolve("start", provider);

            Assert.NotNull(result);
            Assert.Contains("begin", result);
        }

        [Fact]
        public void ResolveScenario_NoSceneMetadata_Skipped()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/nometa.scenario", "text no metadata");

            var result = ScenarioDefs.Resolve("nometa", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_MultipleScenarios_FindsCorrectByName()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/alpha.scenario", ScenarioFile("alpha", "s1", "text A"));
            provider.AddFile("scenarios/beta.scenario", ScenarioFile("beta", "s2", "text B"));

            var result = ScenarioDefs.Resolve("beta", provider);

            Assert.NotNull(result);
            Assert.Contains("text B", result);
        }

        [Fact]
        public void ResolveScenario_EmptyScenarioFile_ReturnsNull()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/empty.scenario", "");

            var result = ScenarioDefs.Resolve("empty", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveScenario_NonScenarioFiles_Ignored()
        {
            var provider = new MockModFileProvider("test");
            provider.AddFile("scenarios/readme.txt", "some text");
            provider.AddFile("scenarios/actual.scenario", ScenarioFile("actual", "scene", "text hello"));

            var result = ScenarioDefs.Resolve("actual", provider);

            Assert.NotNull(result);
            Assert.Contains("hello", result);
        }

        [Fact]
        public void ResolveScenario_PreservesOriginalContent()
        {
            var provider = new MockModFileProvider("test");
            var content = "text \"Line 1\"\ntext \"Line 2\"\nend";
            provider.AddFile("scenarios/multi.scenario", ScenarioFile("multi", "scene", content));

            var result = ScenarioDefs.Resolve("multi", provider);

            Assert.NotNull(result);
            Assert.Contains("Line 1", result);
            Assert.Contains("Line 2", result);
            Assert.Contains("end", result);
        }
    }
}
