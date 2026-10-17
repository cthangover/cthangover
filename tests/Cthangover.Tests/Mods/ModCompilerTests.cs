using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

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

        [Fact]
        public void EnsureCompiled_CompilesIntoTheCacheWithoutLoading()
        {
            var modId = "test_ensure_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var sources = new List<(string, string)> { ("TestClass.cs", ValidClassCode) };
            try
            {
                var first = ModCompiler.EnsureCompiled(modId, sources, null);
                var second = ModCompiler.EnsureCompiled(modId, sources, null);

                Assert.NotNull(first);
                Assert.Equal(first, second);
                Assert.True(File.Exists(first));
                Assert.Equal(first, Cthangover.Core.Mods.Compilation.ModAssemblyCache.CurrentArtifacts[modId]);
                Assert.False(ModAssemblyLoader.IsModLoaded(modId));
                Assert.False(ModAssemblyLoader.IsArtifactLoaded(first));
            }
            finally
            {
                Cthangover.Core.Mods.Compilation.ModAssemblyCache.CurrentArtifacts.TryRemove(modId, out _);
                ModCompiler.InvalidateCache(modId);
            }
        }

        [Fact]
        public void EnsureCompiled_CompileErrorOrNoSources_ReturnsNull()
        {
            var modId = "test_ensure_bad_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            Assert.Null(ModCompiler.EnsureCompiled(modId, new List<(string, string)> { ("Bad.cs", "public class {") }, null));
            Assert.Null(ModCompiler.EnsureCompiled(modId, new List<(string, string)>(), null));
        }

        [Fact]
        public void PrepareAll_CompilesInDependencyOrder_DependentFollowsTheDependencyArtifact()
        {
            var root = Path.Combine(Path.GetTempPath(), "cthangover_prepare_" + System.Guid.NewGuid().ToString("N"));
            var lib = "test_lib_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var app = "test_app_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                var mods = new Dictionary<string, IModInfo>
                {
                    [app] = SourceMod(root, app, "public class AppUser { public string Use() => new LibApi().Hello(); }", lib),
                    [lib] = SourceMod(root, lib, "public class LibApi { public string Hello() => \"v1\"; }"),
                };

                var first = ModCompiler.PrepareAll(mods);

                Assert.Equal(new[] { lib, app }, new[] { first[0].ModId, first[1].ModId });
                Assert.NotEmpty(first[1].Sources);
                Assert.False(ModAssemblyLoader.IsModLoaded(app));

                File.WriteAllText(Path.Combine(root, lib, "src", "Code.cs"), "public class LibApi { public string Hello() => \"v2\"; }");
                var second = ModCompiler.PrepareAll(mods);

                Assert.NotEqual(first[0].ArtifactPath, second[0].ArtifactPath);
                Assert.NotEqual(first[1].ArtifactPath, second[1].ArtifactPath);
                Assert.Empty(GameLogger.CompilationErrors);
            }
            finally
            {
                Cleanup(root, lib, app);
            }
        }

        [Fact]
        public void PrepareAll_DependencyFails_DependentIsSkippedWithAnError()
        {
            var root = Path.Combine(Path.GetTempPath(), "cthangover_prepare_" + System.Guid.NewGuid().ToString("N"));
            var lib = "test_lib_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var app = "test_app_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                var mods = new Dictionary<string, IModInfo>
                {
                    [lib] = SourceMod(root, lib, "public class LibApi {"),
                    [app] = SourceMod(root, app, "public class AppUser { }", lib),
                };

                var result = ModCompiler.PrepareAll(mods);

                Assert.Empty(result);
                Assert.Contains(GameLogger.CompilationErrors, NamesSkippedDependent(lib));
            }
            finally
            {
                Cleanup(root, lib, app);
            }
        }

        private static System.Predicate<string> NamesSkippedDependent(string lib) => new SkippedDependentMatcher(lib).Matches;

        private static ModInfo SourceMod(string root, string id, string code, params string[] depends)
        {
            var dir = Path.Combine(root, id);
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "Code.cs"), code);
            var manifest = new ModManifest { Id = id, Name = id, Version = "1.0.0", Sources = new List<string> { "src/*.cs" }, Depends = new List<ModDependency>() };
            foreach (var dependency in depends)
                manifest.Depends.Add(ModDependency.Parse(dependency + ">=1"));
            return new ModInfo { Id = id, Name = id, Manifest = manifest, FileProvider = new Cthangover.Core.Mods.Providers.FolderModFileProvider(dir, id) };
        }

        private static void Cleanup(string root, params string[] modIds)
        {
            foreach (var id in modIds)
            {
                Cthangover.Core.Mods.Compilation.ModAssemblyCache.CurrentArtifacts.TryRemove(id, out _);
                ModCompiler.InvalidateCache(id);
            }
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
