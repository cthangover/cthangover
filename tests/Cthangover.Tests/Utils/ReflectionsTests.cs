namespace Cthangover.Tests.Utils
{
    using System;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Cthangover.Tests.Mods.Unload;

    /// <summary>
    /// Whole-domain scans see the running game only: an unloaded mod's
    /// assembly lingers in the AppDomain until collected and must not be
    /// rediscovered (it re-registered a released battle core once).
    /// </summary>
    [Collection("mod-load-context")]
    public class ReflectionsTests
    {
        private const string ModSource = @"
namespace ScanMod
{
    public class ScanInit : Cthangover.Core.Mods.IModInitializer
    {
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady() { }
    }
}
";

        [Fact]
        public void LoadedAssemblies_ExcludesReleasedModAssemblies()
        {
            var modId = TestMods.NewId("test_scan");
            var mod = TestMods.Compile(modId, ModSource);

            Assert.Contains(mod, Reflections.LoadedAssemblies());
            Assert.Contains(typeof(Reflections).Assembly, Reflections.LoadedAssemblies());

            ModAssemblyLoader.UnloadMod(modId);

            Assert.DoesNotContain(mod, Reflections.LoadedAssemblies());
        }

        [Fact]
        public void FindAndCreate_WholeDomain_SkipsReleasedModAssemblies()
        {
            var modId = TestMods.NewId("test_scan_create");
            var mod = TestMods.Compile(modId, ModSource);
            var scanType = mod.GetType("ScanMod.ScanInit");

            Assert.Contains(Reflections.FindAndCreate<IModInitializer>(), IsInstanceOf(scanType));
            ModAssemblyLoader.UnloadMod(modId);

            Assert.DoesNotContain(Reflections.FindAndCreate<IModInitializer>(), IsInstanceOf(scanType));
        }

        private static Predicate<IModInitializer> IsInstanceOf(Type type) => type.IsInstanceOfType;
    }
}
