using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cthangover.Tests.Mods.Consumers
{
    /// <summary>
    /// The production load path: a compiled mod is cached as
    /// <c>&lt;id&gt;_&lt;hash&gt;.dll</c> and loaded inside a pipeline batch.
    /// Regression for the file-name-vs-assembly-name lookup that made every
    /// file-backed load fail and silently fall back to a byte load (no PDB,
    /// no collectible context). Also covers the mod test assembly, which must
    /// load into a collectible context to reference the mod it tests.
    /// </summary>
    [Collection("mod-load-context")]
    public class ModAssemblyLoaderContextTests
    {
        private const string MarkerSource = @"
namespace TestBatchMod
{
    public class Marker
    {
        public string Hello() => ""world"";

        public static void Boom()
        {
            throw new System.InvalidOperationException(""boom"");
        }
    }
}
";

        [Fact]
        public void Compile_InBatch_LoadsFileBackedIntoCollectibleContext()
        {
            var modId = "test_batch_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            try
            {
                var assembly = CompileInBatch(modId);

                Assert.NotNull(assembly);
                Assert.True(assembly.IsCollectible, "mod assembly must live in its collectible context");
                Assert.Equal("mod:" + modId, AssemblyLoadContext.GetLoadContext(assembly)?.Name);
                // File-backed: the cache file name carries the content hash,
                // the assembly keeps the mod id — the PDB binds through Location.
                Assert.False(string.IsNullOrEmpty(assembly.Location), "mod assembly must be file-backed (PDB binding)");
                Assert.StartsWith(modId + "_", Path.GetFileName(assembly.Location));
                Assert.True(File.Exists(Path.ChangeExtension(assembly.Location, ".pdb")));

                // The PDB must actually bind: a throw inside mod code carries
                // the mod source file and line in its stack trace.
                var boom = assembly.GetType("TestBatchMod.Marker").GetMethod("Boom");
                var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => boom.Invoke(null, null));
                Assert.Contains("Marker.cs:line ", ex.InnerException.StackTrace);
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(modId);
            }
        }

        [Fact]
        public void LoadTestAssembly_ReferencesCollectibleModAssembly_SharesTypes()
        {
            var modId = "test_batch_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            try
            {
                var production = CompileInBatch(modId);
                Assert.NotNull(production);

                var testDll = EmitTestAssembly(modId, production.Location);
                var tests = ModAssemblyLoader.LoadTestAssembly(testDll, modId);

                Assert.NotNull(tests);
                Assert.True(tests.IsCollectible, "a test assembly referencing a collectible mod must be collectible itself");
                var probe = tests.GetType("TestBatchMod.Tests.Probe");
                var markerType = (Type)probe.GetMethod("MarkerType").Invoke(null, null);
                Assert.Same(production.GetType("TestBatchMod.Marker"), markerType);
                Assert.Equal("world", probe.GetMethod("Call").Invoke(null, null));
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(modId);
            }
        }

        [Fact]
        public void ArtifactsOf_TracksTheFilesAContextRuns()
        {
            var modId = "test_batch_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            try
            {
                var assembly = CompileInBatch(modId);

                Assert.Equal(new[] { assembly.Location }, ModAssemblyLoader.ArtifactsOf(modId));
                Assert.True(ModAssemblyLoader.IsArtifactLoaded(assembly.Location));

                ModAssemblyLoader.UnloadMod(modId);

                Assert.Empty(ModAssemblyLoader.ArtifactsOf(modId));
                Assert.False(ModAssemblyLoader.IsArtifactLoaded(assembly.Location));
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(modId);
            }
        }

        [Fact]
        public void StagePrecompiledDlls_ContentAddressed_LoadStagedIntoTheModContext()
        {
            var modId = "test_dll_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var root = Path.Combine(Path.GetTempPath(), "cthangover_test_mod_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "dll"));
            try
            {
                var mod = new ModInfo { Id = modId, Name = modId, Manifest = new ModManifest { Id = modId }, FileProvider = new FolderModFileProvider(root, modId) };
                EmitLibrary(modId + "_lib", "public class LibV1 { }", Path.Combine(root, "dll", "Lib.dll"));

                var staged = ModAssemblyLoader.StagePrecompiledDlls(new[] { mod });
                var stagedAgain = ModAssemblyLoader.StagePrecompiledDlls(new[] { mod });

                Assert.Single(staged);
                Assert.Equal(modId, staged[0].ModId);
                Assert.StartsWith(modId + "_dll_", Path.GetFileName(staged[0].CachedPath));
                Assert.EndsWith("_Lib.dll", staged[0].CachedPath);
                Assert.True(File.Exists(staged[0].CachedPath));
                Assert.Equal(staged, stagedAgain);

                EmitLibrary(modId + "_lib", "public class LibV2 { }", Path.Combine(root, "dll", "Lib.dll"));
                Assert.NotEqual(staged[0].CachedPath, ModAssemblyLoader.StagePrecompiledDlls(new[] { mod })[0].CachedPath);

                using (ModAssemblyLoader.BeginBatch())
                {
                    var loaded = ModAssemblyLoader.LoadStagedDlls(staged);
                    Assert.Single(loaded);
                    Assert.Equal(modId, loaded[0].ModId);
                    Assert.True(loaded[0].Assembly.IsCollectible);
                    Assert.NotNull(loaded[0].Assembly.GetType("LibV1"));
                }
                Assert.True(ModAssemblyLoader.IsArtifactLoaded(staged[0].CachedPath));
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(modId);
                Directory.Delete(root, recursive: true);
            }
        }

        private static MetadataReference ToReference(string path) => MetadataReference.CreateFromFile(path);

        private static void EmitLibrary(string assemblyName, string source, string path)
        {
            var trustedPlatform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Select(ToReference);
            var compilation = CSharpCompilation.Create(assemblyName, new[] { CSharpSyntaxTree.ParseText(source) }, trustedPlatform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(path);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        }

        private static System.Reflection.Assembly CompileInBatch(string modId)
        {
            // The batch gate is global; this collection runs without
            // parallelism, so no other test compiles inside the window.
            using (ModAssemblyLoader.BeginBatch())
                return ModCompiler.Compile(modId, new[] { ("Marker.cs", MarkerSource) });
        }

        private static string EmitTestAssembly(string modId, string productionPath)
        {
            const string source = @"
namespace TestBatchMod.Tests
{
    public static class Probe
    {
        public static System.Type MarkerType() => typeof(TestBatchMod.Marker);
        public static string Call() => new TestBatchMod.Marker().Hello();
    }
}
";
            var trustedPlatform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Select(p => MetadataReference.CreateFromFile(p));
            var compilation = CSharpCompilation.Create(
                modId + "__tests",
                new[] { CSharpSyntaxTree.ParseText(source) },
                trustedPlatform.Append(MetadataReference.CreateFromFile(productionPath)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var dir = Path.Combine(Path.GetTempPath(), "cthangover_test_asm_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, modId + "__tests_deadbeef.dll");
            var result = compilation.Emit(path);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            return path;
        }
    }
}
