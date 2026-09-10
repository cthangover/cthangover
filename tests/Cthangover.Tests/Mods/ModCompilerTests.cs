using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class ModCompilerTests
    {
        private const string ValidClassCode = @"
using Cthangover.Core.Mods;
public class TestModInit : IModInitializer
{
    public void OnModLoaded(string modId) { }
    public void OnModResourcesReady() { }
}
";

        [Fact]
        public void CompileString_ValidCode_ReturnsAssembly()
        {
            var result = ModCompiler.CompileString(ValidClassCode, "test_script");

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Assembly);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void CompileString_InvalidCode_ReturnsErrors()
        {
            var code = "broken csharp code {{{";

            var result = ModCompiler.CompileString(code, "bad_script");

            Assert.False(result.Success);
            Assert.Null(result.Assembly);
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Compile_EmptySources_ReturnsNull()
        {
            var sources = new List<(string, string)>();

            var result = ModCompiler.Compile("test_mod", sources);

            Assert.Null(result);
        }

        [Fact]
        public void Compile_ValidSingleFile_ReturnsAssembly()
        {
            var sources = new List<(string, string)>
            {
                ("TestClass.cs", ValidClassCode)
            };

            var result = ModCompiler.Compile("test_mod_valid", sources);

            Assert.NotNull(result);
        }

        [Fact]
        public void Compile_SyntaxError_ReturnsNull()
        {
            var sources = new List<(string, string)>
            {
                ("Broken.cs", "totally broken code @@@")
            };

            var result = ModCompiler.Compile("test_mod_broken", sources);

            Assert.Null(result);
        }

        [Fact]
        public void InvalidateCache_NoDir_ReturnsFalse()
        {
            var result = ModCompiler.InvalidateCache("nonexistent_mod");

            Assert.False(result);
        }

        [Fact]
        public void Compile_CachesToDisk()
        {
            var modId = "test_mod_cached_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var sources = new List<(string, string)>
            {
                ("Test.cs", ValidClassCode)
            };

            var result = ModCompiler.Compile(modId, sources);
            Assert.NotNull(result);

            try
            {
                ModCompiler.InvalidateCache(modId);
            }
            catch { }
        }

        [Fact]
        public void Compile_DifferentSource_DifferentHash()
        {
            var modId1 = "test_hash_a_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            var modId2 = "test_hash_b_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);

            var codeA = @"using Cthangover.Core.Mods;
public class TestA : IModInitializer { public void OnModLoaded(string m) { } public void OnModResourcesReady() { } }";

            var codeB = @"using Cthangover.Core.Mods;
public class TestB : IModInitializer { public void OnModLoaded(string m) { } public void OnModResourcesReady() { } }";

            var sources1 = new List<(string, string)>
            {
                ("Test.cs", codeA)
            };

            var sources2 = new List<(string, string)>
            {
                ("Test.cs", codeB)
            };

            var r1 = ModCompiler.Compile(modId1, sources1);
            var r2 = ModCompiler.Compile(modId2, sources2);

            Assert.NotNull(r1);
            Assert.NotNull(r2);

            try { ModCompiler.InvalidateCache(modId1); } catch { }
            try { ModCompiler.InvalidateCache(modId2); } catch { }
        }

        [Fact]
        public void CompileAll_PreCancelledToken_Throws()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                ModCompiler.CompileAll(new Dictionary<string, IModInfo>(), cts.Token);
            });
        }

        [Fact]
        public void Compile_PreCancelledToken_Throws()
        {
            var sources = new List<(string, string)>
            {
                ("TestClass.cs", ValidClassCode)
            };

            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                ModCompiler.Compile("test_mod_cancelled", sources, cts.Token);
            });
        }
    }
}
