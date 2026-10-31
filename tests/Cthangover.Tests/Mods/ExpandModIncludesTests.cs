using Cthangover.Core.Mods;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    using System.Text.Json;
    public class ExpandModIncludesTests
    {
        private static ModManager CreateModManagerWith(MockModFileProvider provider)
        {
            return ModManager.Instance;
        }

        [Fact]
        public void ReadResolvedText_SingleLevel_ResolvesInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${sub.txt}");
            provider.AddFile("sub.txt", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ReadResolvedText_TwoLevel_NestedInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "A ${b.txt}");
            provider.AddFile("b.txt", "B ${c.txt}");
            provider.AddFile("c.txt", "C");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Equal("A B C", result);
        }

        [Fact]
        public void ReadResolvedText_EmptyInclude_PassesThrough()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello ${}", result);
        }

        [Fact]
        public void ReadResolvedText_NotFound_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${nonexistent}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ReadResolvedText_NotFound_JsonFallback()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "Hello ${data}");
            provider.AddFile("data.json", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ReadResolvedText_CircularDirect_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "${a.txt}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ReadResolvedText_CircularIndirect_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("a.txt", "${b.txt}");
            provider.AddFile("b.txt", "${a.txt}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "a.txt", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ReadResolvedText_NoIncludes_ReturnsSame()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("plain.txt", "Just plain text");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "plain.txt", provider);

            Assert.Equal("Just plain text", result);
        }

        [Fact]
        public void ReadResolvedText_NullContent_ReturnsNull()
        {
            var provider = new MockModFileProvider("test_mod");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "nonexistent.txt", provider);

            Assert.Null(result);
        }

        [Fact]
        public void ReadResolvedText_MultipleIncludes_InSameLine()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("main.txt", "${a.txt} ${b.txt}");
            provider.AddFile("a.txt", "Hello");
            provider.AddFile("b.txt", "World");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "main.txt", provider);

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ReadResolvedText_IncludeWithRelativePath()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("dir/main.txt", "${../shared/data.txt}");
            provider.AddFile("shared/data.txt", "Resolved");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "dir/main.txt", provider);

            Assert.Equal("Resolved", result);
        }

        [Fact]
        public void ReadResolvedText_JsonScenarioVariables_ArePreserved()
        {
            var provider = new MockModFileProvider("test_mod");
            var commands = new[] { "cset reward \"${money}+0\"", "action item.add id=money/coin count=${reward}", "ptext \"${reward} coins\"" };
            var source = JsonSerializer.Serialize(commands);
            provider.AddFile("events.json", source);
            // Even an existing file must not shadow a runtime variable inside a JSON string.
            provider.AddFile("money.json", "99");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            Assert.Equal(source, result);
            Assert.Equal(commands, JsonSerializer.Deserialize<string[]>(result));
        }

        [Fact]
        public void ReadResolvedText_JsonIncludeAndVariables_ResolveAtDifferentStages()
        {
            var provider = new MockModFileProvider("test_mod");
            provider.AddFile("events.json", "{\"Events\": [${chest}], \"Text\": \"${reward}\"}");
            provider.AddFile("chest.json", "{\"Commands\": [\"ptext \\\"${money}\\\"\", \"action item.add count=${reward}\"]}");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            using var json = JsonDocument.Parse(result);
            Assert.Equal("${reward}", json.RootElement.GetProperty("Text").GetString());
            var commands = json.RootElement.GetProperty("Events")[0].GetProperty("Commands");
            Assert.Equal("ptext \"${money}\"", commands[0].GetString());
            Assert.Equal("action item.add count=${reward}", commands[1].GetString());
        }

        [Fact]
        public void ReadResolvedText_JsonEscapes_DoNotHideFollowingInclude()
        {
            var provider = new MockModFileProvider("test_mod");
            var literal = "path\\\"quoted ${reward}\\";
            provider.AddFile("events.json", "{\"Text\":" + JsonSerializer.Serialize(literal) + ",\"Data\":${data.json}}");
            provider.AddFile("data.json", "[1,2]");

            var result = ModManager.Instance.ReadResolvedText("test_mod", "events.json", provider);

            using var json = JsonDocument.Parse(result);
            Assert.Equal(literal, json.RootElement.GetProperty("Text").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("Data")[1].GetInt32());
        }
    }
}
