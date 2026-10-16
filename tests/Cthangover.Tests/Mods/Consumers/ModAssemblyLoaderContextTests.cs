using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Cthangover.Core.Mods;
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
