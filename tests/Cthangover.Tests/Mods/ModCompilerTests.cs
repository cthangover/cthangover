using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Cthangover.Core.Mods.Compilation;
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Compiler artifacts are checked through emitted assemblies, portable symbols and cache identities;
    /// cancellation and dependency failures must preserve the existing prepare-before-load boundary.
    /// </summary>
    public class ModCompilerTests : IDisposable
    {
        private readonly List<string> ownedArtifacts = new();
        private readonly List<string> ownedModIds = new();

        private System.Threading.CancellationToken cancelledToken;

        private List<(string, string)> cancelledSources;
        private const string ValidClassCode = @"
using Cthangover.Core.Mods;
public class TestModInit : IModInitializer
{
    public void OnModLoaded(string modId) { }
    public void OnModResourcesReady() { }
}
";
        /// <summary>
        /// Ad-hoc valid code remains executable without entering the persistent mod artifact cache.
        /// </summary>
        [Fact]
        public void CompileString_ValidCode_ReturnsAssembly()
        {
            var result = ModCompiler.CompileString(ValidClassCode, "test_script");
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Assembly);
            Assert.Empty(result.Errors);
        }

        /// <summary>
        /// Ad-hoc syntax errors return diagnostics and no partially usable assembly.
        /// </summary>
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

        /// <summary>
        /// A content-only mod creates no empty assembly artifact.
        /// </summary>
        [Fact]
        public void Compile_EmptySources_ReturnsNull()
        {
            var sources = new List<(string, string)>();
            var result = ModCompiler.Compile("test_mod", sources);
            Assert.Null(result);
        }

        /// <summary>
        /// A normal source mod produces a usable assembly through the public load path.
        /// </summary>
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

        /// <summary>
        /// A broken normal mod cannot return a runnable assembly.
        /// </summary>
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

        /// <summary>
        /// Invalidating an absent identity does not report that live artifacts were replaced.
        /// </summary>
        [Fact]
        public void InvalidateCache_NoDir_ReturnsFalse()
        {
            var result = ModCompiler.InvalidateCache("nonexistent_mod");
            Assert.False(result);
        }

        /// <summary>
        /// A valid runtime mod is compiled through the persistent cache: the
        /// artifact pair (DLL + named PDB) must exist on disk under the cache
        /// identity, and invalidation must report the artifacts it retires.
        /// </summary>
        [Fact]
        public void Compile_CachesToDisk()
        {
            var modId = "test_mod_cached_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var sources = new List<(string, string)>
            {
                ("Test.cs", ValidClassCode)
            };
            var assembly = ModCompiler.Compile(modId, sources);
            Assert.NotNull(assembly);

            Assert.True(ModAssemblyCache.CurrentArtifacts.TryGetValue(modId, out var artifact),
                "compiled mod must be registered in the artifact map");
            Assert.True(File.Exists(artifact), $"cache DLL missing on disk: {artifact}");
            Assert.True(File.Exists(Path.ChangeExtension(artifact, ".pdb")),
                "portable PDB must be stored next to the cached DLL");
            Assert.True(ModAssemblyCache.IsSelfCacheDll(Path.GetFileName(artifact), modId),
                $"artifact name must follow <modId>_<hash>.dll, was: {Path.GetFileName(artifact)}");

            Assert.True(ModCompiler.InvalidateCache(modId),
                "invalidation must report retiring the artifacts it just wrote");
        }

        /// <summary>
        /// Under the SAME cache identity, a changed source must produce a
        /// different content hash — a distinct artifact file, with both pairs
        /// preserved on disk — otherwise hot reload would keep loading the
        /// stale assembly. Using two different mod ids would hide exactly that
        /// regression, because the id itself is part of the cache path.
        /// </summary>
        [Fact]
        public void Compile_DifferentSource_DifferentHash()
        {
            var modId = "test_hash_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var codeA = @"using Cthangover.Core.Mods;
public class TestA : IModInitializer { public void OnModLoaded(string m) { } public void OnModResourcesReady() { } }";
            var codeB = @"using Cthangover.Core.Mods;
public class TestB : IModInitializer { public void OnModLoaded(string m) { } public void OnModResourcesReady() { } }";

            var first = ModCompiler.Compile(modId, new List<(string, string)> { ("Test.cs", codeA) });
            Assert.NotNull(first);
            Assert.True(ModAssemblyCache.CurrentArtifacts.TryGetValue(modId, out var artifactA));
            Assert.True(File.Exists(artifactA));

            var second = ModCompiler.Compile(modId, new List<(string, string)> { ("Test.cs", codeB) });
            Assert.NotNull(second);
            Assert.True(ModAssemblyCache.CurrentArtifacts.TryGetValue(modId, out var artifactB));

            Assert.NotEqual(artifactA, artifactB);
            Assert.NotEqual(Path.GetFileName(artifactA), Path.GetFileName(artifactB));
            Assert.True(File.Exists(artifactB));
            Assert.True(File.Exists(artifactA), "the earlier artifact pair must not be deleted by the recompile");

            Assert.True(ModCompiler.InvalidateCache(modId));
        }

        /// <summary>
        /// Cancellation before a preparation batch prevents compilation instead of reporting success.
        /// </summary>
        [Fact]
        public void CompileAll_PreCancelledToken_Throws()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            cancelledToken = cts.Token;
            Assert.Throws<OperationCanceledException>(CompileAllCancelled);
        }

        /// <summary>
        /// Cancellation before a single compilation prevents artifact creation.
        /// </summary>
        [Fact]
        public void Compile_PreCancelledToken_Throws()
        {
            var sources = new List<(string, string)>
            {
                ("TestClass.cs", ValidClassCode)
            };
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            cancelledToken = cts.Token;
            cancelledSources = sources;
            Assert.Throws<OperationCanceledException>(CompileCancelled);
        }

        /// <summary>
        /// Repeated preparation reuses the same artifact and records it without loading a mod assembly.
        /// </summary>
        [Fact]
        public void EnsureCompiled_CompilesIntoTheCacheWithoutLoading()
        {
            var modId = "test_ensure_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var sources = new List<(string, string)>
            {
                ("TestClass.cs", ValidClassCode)
            };
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

        /// <summary>
        /// Both malformed sources and a content-only selection refuse to produce a prepared artifact.
        /// </summary>
        [Fact]
        public void EnsureCompiled_CompileErrorOrNoSources_ReturnsNull()
        {
            var modId = "test_ensure_bad_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Assert.Null(ModCompiler.EnsureCompiled(modId, new List<(string, string)> { ("Bad.cs", "public class {") }, null));
            Assert.Null(ModCompiler.EnsureCompiled(modId, new List<(string, string)>(), null));
        }

        /// <summary>
        /// A changed provider prepares first and changes its dependent artifact before either assembly loads.
        /// </summary>
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
                Assert.Equal(new[] { 
                    lib, 
                    app 
                }, new[] { 
                    first[0].ModId, 
                    first[1].ModId 
                });
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

        /// <summary>
        /// A failed provider prevents its dependent from compiling against stale output and names that dependency.
        /// </summary>
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

        /// <summary>
        /// Runtime output permits JIT optimization without an attached debugger, retains portable source documents,
        /// and cannot reuse the former cache identity that omitted compilation optimization.
        /// </summary>
        [Fact]
        public void EnsureCompiled_OptimizationMetadataAndPortableSymbolsMatchRuntimePolicy()
        {
            var id = "test_optimized_" + Guid.NewGuid().ToString("N");
            ownedModIds.Add(id);
            var sources = new List<(string FilePath, string Source)>
            {
                ("TestClass.cs", ValidClassCode)
            };
            var generators = GodotGeneratorHost.GetGodotGenerators();
            var legacy = ModAssemblyCache.BuildCachePath(id, sources, ";script-root=managed", generators != null && generators.Length > 0);
            var artifact = ModCompiler.EnsureCompiled(id, sources, null);
            Assert.NotNull(artifact);
            ownedArtifacts.Add(artifact);
            Assert.NotEqual(legacy, artifact);
            Assert.Equal(Debugger.IsAttached, (ReadDebugModes(artifact) & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0);
            Assert.False(ModAssemblyLoader.IsArtifactLoaded(artifact));
            var symbols = Path.ChangeExtension(artifact, ".pdb");
            Assert.True(File.Exists(symbols));
            using var stream = File.OpenRead(symbols);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            var reader = provider.GetMetadataReader();
            var sourceFound = false;
            foreach (var handle in reader.Documents)
            {
                var document = reader.GetDocument(handle);
                sourceFound |= reader.GetString(document.Name).EndsWith("TestClass.cs", StringComparison.Ordinal);
            }

            Assert.True(sourceFound, "optimized artifacts retain their portable source documents");
            Assert.Equal(artifact, ModCompiler.EnsureCompiled(id, sources, null));
        }

        /// <summary>
        /// Source authors can still request an explicitly unoptimized assembly; the runtime compiler does not
        /// overwrite their Debuggable attribute while optimizing ordinary mods.
        /// </summary>
        [Fact]
        public void EnsureCompiled_AuthoredDebugAttributeRemainsEffective()
        {
            var id = "test_authored_debug_" + Guid.NewGuid().ToString("N");
            ownedModIds.Add(id);
            var code = "using System.Diagnostics; " + "[assembly: Debuggable(DebuggableAttribute.DebuggingModes.DisableOptimizations)] " 
                + "public sealed class DebugSample {}";
            var artifact = ModCompiler.EnsureCompiled(id, new[] { ("DebugSample.cs", code) }, null);
            Assert.NotNull(artifact);
            ownedArtifacts.Add(artifact);
            Assert.True((ReadDebugModes(artifact) & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0);
        }

        /// <summary>
        /// The in-game script editor keeps its existing unoptimized compilation path for local inspection.
        /// </summary>
        [Fact]
        public void CompileString_KeepsDebuggableLocalsPolicy()
        {
            var result = ModCompiler.CompileString(ValidClassCode, "test_script_debug_policy");
            Assert.True(result.Success);
            var attribute = result.Assembly.GetCustomAttribute<DebuggableAttribute>();
            Assert.NotNull(attribute);
            Assert.True(attribute.IsJITOptimizerDisabled);
        }

        private static DebuggableAttribute.DebuggingModes ReadDebugModes(string path)
        {
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            var reader = image.GetMetadataReader();
            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }

                var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                if (reader.GetString(type.Namespace) != "System.Diagnostics" || reader.GetString(type.Name) != "DebuggableAttribute")
                {
                    continue;
                }

                var value = reader.GetBlobReader(attribute.Value);
                Assert.Equal((ushort)1, value.ReadUInt16());
                return (DebuggableAttribute.DebuggingModes)value.ReadInt32();
            }

            throw new InvalidDataException("Compiled assembly has no Debuggable attribute");
        }

        private void CompileAllCancelled()
        {
            ModCompiler.CompileAll(new Dictionary<string, IModInfo>(), cancelledToken);
        }

        private void CompileCancelled()
        {
            ModCompiler.Compile("test_mod_cancelled", cancelledSources, cancelledToken);
        }

        /// <summary>
        /// Removes this case's unique, never-loaded optimization fixtures after releasing their cache bindings;
        /// existing dependency fixtures retain their own try/finally cleanup.
        /// </summary>
        public void Dispose()
        {
            foreach (var id in ownedModIds)
            {
                ModAssemblyCache.CurrentArtifacts.TryRemove(id, out _);
                ModCompiler.InvalidateCache(id);
            }

            foreach (var artifact in ownedArtifacts)
            {
                File.Delete(artifact);
                File.Delete(Path.ChangeExtension(artifact, ".pdb"));
            }
        }

        private static System.Predicate<string> NamesSkippedDependent(string lib)
        {
            return new SkippedDependentMatcher(lib).Matches;
        }

        private static ModInfo SourceMod(string root, string id, string code, params string[] depends)
        {
            var dir = Path.Combine(root, id);
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "Code.cs"), code);
            var manifest = new ModManifest
            {
                Id = id,
                Name = id,
                Version = "1.0.0",
                Sources = new List<string>
                {
                    "src/*.cs"
                },
                Depends = new List<ModDependency>()
            };
            foreach (var dependency in depends)
            {
                manifest.Depends.Add(ModDependency.Parse(dependency + ">=1"));
            }

            return new ModInfo
            {
                Id = id,
                Name = id,
                Manifest = manifest,
                FileProvider = new Cthangover.Core.Mods.Providers.FolderModFileProvider(dir, id)
            };
        }

        private static void Cleanup(string root, params string[] modIds)
        {
            foreach (var id in modIds)
            {
                Cthangover.Core.Mods.Compilation.ModAssemblyCache.CurrentArtifacts.TryRemove(id, out _);
                ModCompiler.InvalidateCache(id);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
