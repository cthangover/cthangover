namespace Engine.Tests.Boot
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// Resettables of mods that leave the set (plan 3.1): the unload pass drops
    /// the entries whose code is in a dying assembly, and a reloaded mod's new
    /// owner of the same type replaces the old entry instead of being ignored
    /// (the old one would reset state nobody reads and keep its context alive).
    /// Shares the "appservices" collection because it publishes a service set.
    /// </summary>
    [Collection("appservices")]
    public class ResettableUnloadTests
    {
        [Fact]
        public void RemoveOwnedResettables_DropsOnlyTheDyingAssembly()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var owned = new OwnedOwner("mod_state");
                services.RegisterResettable(owned);
                var before = services.Resettables.Count;

                var removed = services.RemoveOwnedResettables(new AssemblyOwnership(new[] { typeof(ResettableUnloadTests).Assembly }));

                Assert.Equal(1, removed);
                Assert.Equal(before - 1, services.Resettables.Count);
                Assert.DoesNotContain(services.Resettables, r => ReferenceEquals(r, owned));
                // The core's own owner is a delegate declared in Core, which is not dying.
                Assert.Contains(services.Resettables, r => r.Id == "map_zones");
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RemoveOwnedResettables_EmptyOwnership_RemovesNothing()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                services.RegisterResettable(new OwnedOwner("mod_state"));
                var before = services.Resettables.Count;

                Assert.Equal(0, services.RemoveOwnedResettables(new AssemblyOwnership(Array.Empty<Assembly>())));
                Assert.Equal(before, services.Resettables.Count);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_SameTypeFromReloadedAssembly_ReplacesOldInstance()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // Two assemblies, each defining the same full type name: the second
                // one stands for the same mod loaded again (a game switch or reload).
                var oldOwner = (ProbeOwner)Activator.CreateInstance(CreateProbeType("probe_old"));
                var newOwner = (ProbeOwner)Activator.CreateInstance(CreateProbeType("probe_new"));
                Assert.NotSame(oldOwner.GetType().Assembly, newOwner.GetType().Assembly);
                Assert.Equal(oldOwner.GetType().FullName, newOwner.GetType().FullName);

                services.RegisterResettable(oldOwner);
                services.RegisterResettable(newOwner);

                var entries = services.Resettables.Where(r => r.Id == "stats").ToList();
                Assert.Single(entries);
                Assert.Same(newOwner, entries[0]);

                services.ResetForNewGame();
                Assert.Equal(1, newOwner.ResetCount);
                Assert.Equal(0, oldOwner.ResetCount);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        private static Type CreateProbeType(string assemblyName)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Main");
            var type = module.DefineType("Mods.Probe.StatsOwner", TypeAttributes.Public | TypeAttributes.Class, typeof(ProbeOwner));
            return type.CreateType();
        }

        /// <summary>Base for the probe types; the dynamic subclasses add no members.</summary>
        public abstract class ProbeOwner : IResettable
        {
            public virtual string Id => "stats";

            public int ResetCount { get; private set; }

            public void ResetForNewGame() => ResetCount++;
        }

        private sealed class OwnedOwner : IResettable
        {
            public OwnedOwner(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public void ResetForNewGame()
            {
            }
        }
    }
}
