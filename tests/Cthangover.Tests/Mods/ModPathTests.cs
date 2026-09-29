using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class ModPathTests
    {
        [Theory]
        [InlineData("a\\b\\c", "a/b/c")]
        [InlineData("/a/b", "a/b")]
        [InlineData("a/./b", "a/b")]
        [InlineData("a/b/../c", "a/c")]
        [InlineData("../a", "a")]
        [InlineData("a/../../b", "b")]
        [InlineData("C:/a/b", "a/b")]
        [InlineData("dir/", "dir")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Normalize_ProducesCanonicalRelativePath(string input, string expected)
        {
            Assert.Equal(expected, ModPath.Normalize(input));
        }

        [Theory]
        [InlineData("patches/character", "patches/character/")]
        [InlineData("patches/character/", "patches/character/")]
        [InlineData("patches\\character", "patches/character/")]
        [InlineData("", "")]
        public void NormalizeDirectory_AppendsTrailingSlash(string input, string expected)
        {
            Assert.Equal(expected, ModPath.NormalizeDirectory(input));
        }

        [Theory]
        [InlineData("a/b/c.json", "a/b")]
        [InlineData("c.json", "")]
        [InlineData("a/../b/c.json", "b")]
        public void GetDirectory_ReturnsCanonicalParent(string input, string expected)
        {
            Assert.Equal(expected, ModPath.GetDirectory(input));
        }

        [Theory]
        [InlineData("../shared/data.json", "dir/main.txt", "shared/data.json")]
        [InlineData("sub/x.json", "dir/main.txt", "dir/sub/x.json")]
        [InlineData("data.json", "main.txt", "data.json")]
        [InlineData("../../outside.json", "a/b.txt", "outside.json")]
        public void ResolveRelative_CollapsesParentSegments(string includePath, string currentPath, string expected)
        {
            Assert.Equal(expected, ModPath.ResolveRelative(includePath, currentPath));
        }
    }
}
