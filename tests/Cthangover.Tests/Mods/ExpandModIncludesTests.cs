using Cthangover.Core.Mods;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
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
    }
}
