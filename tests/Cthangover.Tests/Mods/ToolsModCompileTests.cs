using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Repository-level compile guard for the tools mod. Mod code is excluded
    /// from the main csproj and only compiled by Roslyn at runtime, so a syntax
    /// or reference error in <c>mods/tools/src</c> would otherwise surface only
    /// when the game boots. Compiling the real sources here keeps that failure
    /// in the test run. The check is skipped when the tools mod is not part of
    /// the checkout, so extractions without mods do not fail.
    /// </summary>
    public class ToolsModCompileTests
    {
        [Fact]
        public void Compile_RealToolsMod_Succeeds()
        {
            var sourceRoot = FindToolsSourceRoot();
            if (sourceRoot == null)
                return;

            var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            var sources = files.Select(file => (file, File.ReadAllText(file))).ToList();

            var assembly = ModCompiler.Compile("tools_compile_test", sources);

            if (assembly == null)
                Assert.Fail(string.Join("\n", GameLogger.CompilationErrors));

            Assert.NotNull(assembly);
        }

        /// <summary>
        /// Walks up from the test binaries to the repository root and returns
        /// <c>mods/tools/src</c>, or <c>null</c> when this checkout has no
        /// tools mod.
        /// </summary>
        private static string FindToolsSourceRoot()
        {
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "mods", "tools", "src");
                if (Directory.Exists(candidate))
                    return candidate;

                if (File.Exists(Path.Combine(directory.FullName, "Cthangover.csproj")))
                    return null;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
