namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// The static reference sweep against a real compiled mod: references to
    /// mod code leave the surviving statics (and the objects behind them),
    /// everything else stays.
    /// </summary>
    [Collection("mod-load-context")]
    public class StaticReferenceSweeperTests : IDisposable
    {
        private const string ModSource = @"
namespace SweepMod
{
    public class Marker { }

    public static class Api
    {
        public static void Ping() { }
    }
}
";

        private readonly string modId = TestMods.NewId("test_sweep");
        private readonly Assembly mod;

        public StaticReferenceSweeperTests()
        {
            SweepHostSlots.Reset();
            mod = TestMods.Compile(modId, ModSource);
        }

        public void Dispose()
        {
            SweepHostSlots.Reset();
            ModAssemblyLoader.UnloadMod(modId);
        }

        private object Marker() => Activator.CreateInstance(mod.GetType("SweepMod.Marker"));

        private Action Ping() => (Action)Delegate.CreateDelegate(typeof(Action), mod.GetType("SweepMod.Api").GetMethod("Ping"));

        [Fact]
        public void Sweep_ClearsModReferences_KeepsForeignOnes()
        {
            Action foreign = GC.Collect;
            SweepHostSlots.Callback = (Action)Delegate.Combine(Ping(), foreign);
            SweepHostSlots.Slot = Marker();
            SweepHostSlots.Items.Add(Marker());
            SweepHostSlots.Items.Add("keep");
            SweepHostSlots.ByName["mod"] = Marker();
            SweepHostSlots.ByName["type"] = mod.GetType("SweepMod.Marker");
            SweepHostSlots.ByName["keep"] = 1;
            SweepHostSlots.Set.Add(Marker());
            SweepHostSlots.Set.Add("keep");
            SweepHostSlots.Registry.Add("x", Marker());
            SweepHostSlots.Registry.Add("x", "keep");

            var report = StaticReferenceSweeper.Sweep(new AssemblyOwnership(new[] { mod }), new[] { typeof(SweepHostSlots).Assembly });

            Assert.Equal(new Delegate[] { foreign }, SweepHostSlots.Callback.GetInvocationList());
            Assert.Null(SweepHostSlots.Slot);
            Assert.Equal(new object[] { "keep" }, SweepHostSlots.Items);
            Assert.Equal(new[] { "keep" }, SweepHostSlots.ByName.Keys.ToArray());
            Assert.Equal(new object[] { "keep" }, SweepHostSlots.Set.ToArray());
            Assert.Equal(1, SweepHostSlots.Registry.Count("x"));
            Assert.Contains(report, NamesSlotField);
        }

        private static bool NamesSlotField(string line) => line.Contains(nameof(SweepHostSlots) + "." + nameof(SweepHostSlots.Slot));

        [Fact]
        public void Sweep_EmptyOwnership_ChangesNothing()
        {
            SweepHostSlots.Slot = Marker();
            SweepHostSlots.Items.Add(Marker());

            var report = StaticReferenceSweeper.Sweep(new AssemblyOwnership(null), new[] { typeof(SweepHostSlots).Assembly });

            Assert.Empty(report);
            Assert.NotNull(SweepHostSlots.Slot);
            Assert.Single(SweepHostSlots.Items);
        }

        [Fact]
        public void Sweep_ExplicitInstances_AreWalkedWithoutHostAssemblies()
        {
            var registry = new SweepHostRegistry();
            registry.Add("x", Marker());
            registry.Add("x", "keep");

            StaticReferenceSweeper.Sweep(new AssemblyOwnership(new[] { mod }), Array.Empty<Assembly>(), new object[] { registry });

            Assert.Equal(1, registry.Count("x"));
        }
    }
}
