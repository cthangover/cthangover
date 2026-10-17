using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Cthangover.Core.Mods;
using Xunit;

namespace Cthangover.Tests.Mods.Consumers
{
    /// <summary>
    /// Collectible context lifecycle: a compiled mod assembly loads into its
    /// own context, its types work, and the context releases once nothing
    /// references it. The whole cycle runs in one non-inlined frame because a
    /// Debug-JIT local would pin the ALC across the GC otherwise — the
    /// classic ALC test trap. The mod id is unique per run so parallel test
    /// classes compiling other mods never race on this cache artifact.
    /// </summary>
    [Collection("mod-load-context")]
    public class ModLoadContextTests
    {
        [Fact]
        public void LoadIntoContext_TypeWorks_AndUnloadReleases()
        {
            var modId = "test_alc_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var sources = new[] { ("Marker.cs", @"
namespace TestAlcMod
{
    public class Marker
    {
        public string Hello() => ""world"";
    }
}
") };
            var assembly = ModCompiler.Compile(modId, sources);
            Assert.NotNull(assembly);

            // Route it into a collectible context. dllPath taken without
            // keeping the default-context assembly reference alive.
            var dllPath = assembly.Location;
            Assert.True(File.Exists(dllPath));

            var weak = RunLoadAndUnload(modId, dllPath);

            // Verification runs in a frame with no live ALC-rooted locals:
            // any surviving Assembly object from the context would keep the
            // ALC instance alive and the weak reference would never die.
            var diagnostic = ModLoadContext.CollectAndVerify(weak, modId + "_2");
            Assert.Null(diagnostic);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RunLoadAndUnload(string modId, string dllPath)
        {
            // The test drives the collectible path directly (no boot pipeline
            // here): the context is created explicitly, not through the loader
            // batch gate.
            var context = new ModLoadContext(modId + "_2", _ => null);
            context.StageAssembly(dllPath);

            var weak = new WeakReference(context);

            // Resolve by the internal assembly name (== modId for compiled
            // mods), not by the cache file name.
            var loaded = context.LoadStaged(modId);
            Assert.NotNull(loaded);
            var markerType = loaded.GetType("TestAlcMod.Marker");
            Assert.NotNull(markerType);
            var call = (string)markerType.GetMethod("Hello").Invoke(Activator.CreateInstance(markerType), null);
            Assert.Equal("world", call);

            context.TryUnload();
            return weak;
        }

        [Fact]
        public void UnloadMod_UnknownId_IsNoOp()
        {
            Assert.Null(ModAssemblyLoader.UnloadMod("no_such_mod_anywhere"));
            Assert.False(ModAssemblyLoader.IsModLoaded("no_such_mod_anywhere"));
        }

        [Fact]
        public void BeginBatch_EndBatch_IsBalanced()
        {
            // The gate is a simple boolean flag: the batch owner opens and
            // closes it around the pipeline phases. After a balanced pair an
            // unknown-id unload stays a no-op.
            ModAssemblyLoader.BeginBatch();
            ModAssemblyLoader.EndBatch();

            Assert.Null(ModAssemblyLoader.UnloadMod("no_such_mod_anywhere"));
        }

        [Fact]
        public void TryUnload_MarksTheContextAndItsAssembliesReleased()
        {
            var modId = Cthangover.Tests.Mods.Unload.TestMods.NewId("test_released");
            var compiled = Cthangover.Tests.Mods.Unload.TestMods.Compile(modId, "namespace ReleasedMod { public class Marker { } }");
            var context = (ModLoadContext)System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(compiled);

            Assert.False(context.IsReleased);
            Assert.False(ModLoadContext.IsReleasedAssembly(compiled));

            ModAssemblyLoader.UnloadMod(modId);

            Assert.True(context.IsReleased);
            Assert.True(ModLoadContext.IsReleasedAssembly(compiled));
            Assert.False(ModLoadContext.IsReleasedAssembly(typeof(ModLoadContext).Assembly));
            Assert.False(ModLoadContext.IsReleasedAssembly(null));
        }
    }
}
