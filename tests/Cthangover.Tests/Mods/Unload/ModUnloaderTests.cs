namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// The unload protocol end to end on a compiled mod: the context is
    /// released and collected, references from surviving statics are swept
    /// first. Everything touching the mod assembly runs in non-inlined helpers
    /// so no test-method local keeps the context alive.
    /// </summary>
    [Collection("mod-load-context")]
    public class ModUnloaderTests : IDisposable
    {
        private const string ModSource = @"
namespace UnloadMod
{
    public static class Api
    {
        public static void Ping() { }
    }
}
";

        public ModUnloaderTests() => SweepHostSlots.Reset();

        public void Dispose() => SweepHostSlots.Reset();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LoadMod(string modId, bool parkDelegateInHost)
        {
            var assembly = TestMods.Compile(modId, ModSource);
            if (parkDelegateInHost)
                SweepHostSlots.Callback = (Action)Delegate.CreateDelegate(typeof(Action), assembly.GetType("UnloadMod.Api").GetMethod("Ping"));
        }

        [Fact]
        public void Unload_ReleasesAndCollectsTheContext()
        {
            var modId = TestMods.NewId("test_unload");
            LoadMod(modId, parkDelegateInHost: false);
            Assert.True(ModAssemblyLoader.IsModLoaded(modId));

            var ticket = ModUnloader.Unload(new[] { modId }, null, Array.Empty<Assembly>());

            Assert.Equal(new[] { modId }, ticket.ModIds);
            Assert.False(ModAssemblyLoader.IsModLoaded(modId));
            Assert.Empty(ticket.Collect(30));
        }

        [Fact]
        public void Unload_SweepsHostStatics_ThenTheContextIsCollected()
        {
            var modId = TestMods.NewId("test_unload_host");
            LoadMod(modId, parkDelegateInHost: true);
            Assert.NotNull(SweepHostSlots.Callback);

            var ticket = ModUnloader.Unload(new[] { modId }, null, new[] { typeof(SweepHostSlots).Assembly });

            Assert.Null(SweepHostSlots.Callback);
            Assert.Empty(ticket.Collect(30));
        }

        [Fact]
        public void Unload_UnknownMod_ReturnsAnEmptyTicket()
        {
            var ticket = ModUnloader.Unload(new[] { "no_such_mod_anywhere" }, null, Array.Empty<Assembly>());

            Assert.Empty(ticket.ModIds);
        }

        [Fact]
        public void UnregisterKept_LeavesTheModLoaded()
        {
            var modId = TestMods.NewId("test_unload_kept");
            LoadMod(modId, parkDelegateInHost: false);
            var artifacts = ModAssemblyLoader.ArtifactsOf(modId);

            ModUnloader.UnregisterKept(new[] { modId });

            Assert.True(ModAssemblyLoader.IsModLoaded(modId));
            Assert.Equal(artifacts, ModAssemblyLoader.ArtifactsOf(modId));
            ModAssemblyLoader.UnloadMod(modId);
        }
    }
}
