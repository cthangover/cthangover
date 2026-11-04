namespace Cthangover.Tests.Testing
{
    using System;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Malformed test declarations must produce actionable discovery errors, never an empty green run.</summary>
    public class ModDiscoveryContractTests
    {
        [Fact]
        public void StaticTestClassesAreDiscovered()
        {
            var assembly = ModTestAssembly.Compile("public static class Example { [ModTest(\"group/case\")] public static void Case(ModTestContext ctx) {} }");
            Assert.Equal("group/case", Assert.Single(ModTestDiscovery.Discover(assembly)).Name);
        }

        [Theory]
        [InlineData("[ModTest(null)] public static void Case() {}")]
        [InlineData("[ModTest(\"\")] public static void Case() {}")]
        [InlineData("[ModTest(\"case\")] public void Case() {}")]
        [InlineData("[ModTest(\"case\")] public static int Case() { return 1; }")]
        [InlineData("[ModTest(\"case\")] public static void Case(int wrong) {}")]
        [InlineData("[ModTest(\"case\")] public static void Case<T>() {}")]
        [InlineData("[ModTest(\"case\")] public static async void Case() { await Task.CompletedTask; }")]
        [InlineData("[ModTest(\"case\", TimeoutSeconds=0)] public static void Case() {}")]
        [InlineData("[ModTest(\"case\")] public static void One() {} [ModTest(\"case\")] public static void Two() {}")]
        public void InvalidDeclarationsAreRejected(string declaration)
        {
            var assembly = ModTestAssembly.Compile("public class Example { " + declaration + " }");
            Assert.Throws<InvalidOperationException>(() => ModTestDiscovery.Discover(assembly));
        }

        [Theory]
        [InlineData("followers/turn", "followers/*", true)]
        [InlineData("followers/turn", "followers/join", false)]
        [InlineData("[literal]", "[literal]", true)]
        [InlineData("inventory/add", "followers/*,inventory/?dd", true)]
        public void FiltersUseLiteralGlobs(string name, string filter, bool expected)
            => Assert.Equal(expected, ModTestFilter.Matches(name, filter));

        [Fact]
        public void NoExecutedOrSkippedChecksCannotBeSuccessful()
            => Assert.False(ModTestReportWriter.ComputeTotals(Array.Empty<ModTestSuiteSummary>()).Success);
    }
}
