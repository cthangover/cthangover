namespace Cthangover.Tests.Events
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Unload;
    using Cthangover.Tests.Mods.Unload;

    /// <summary>Typed subscriptions: explicit unsubscribe and the unload pass by code ownership.</summary>
    [Collection("mod-load-context")]
    public class ModEventBusTests
    {
        private const string ModSource = @"
namespace BusMod
{
    public static class Handlers
    {
        public static int Hits;

        public static void OnDefeated(Cthangover.Core.Events.CharactersDefeatedEvent e)
        {
            if (e.Source == ""bus_test"")
                Hits++;
        }
    }
}
";

        private int probeHits;

        private void OnProbe(BusProbeEvent e) => probeHits++;

        [Fact]
        public void Unsubscribe_StopsDelivery()
        {
            ModEventBus.Subscribe<BusProbeEvent>(OnProbe);
            ModEventBus.Publish(new BusProbeEvent());
            ModEventBus.Unsubscribe<BusProbeEvent>(OnProbe);
            ModEventBus.Publish(new BusProbeEvent());

            Assert.Equal(1, probeHits);
        }

        [Fact]
        public void LifecycleEvent_OrdersByPriority_KeepingSubscriptionOrderForTies()
        {
            var log = new List<int>();
            var probes = new List<BusOrderProbe>();
            for (var i = 0; i < 40; i++)
            {
                var probe = new BusOrderProbe(i, log);
                probes.Add(probe);
                ModEventBus.Subscribe<BusLifecycleProbeEvent>(probe.Handle, priority: i % 3);
            }

            ModEventBus.Publish(new BusLifecycleProbeEvent());
            foreach (var probe in probes)
                ModEventBus.Unsubscribe<BusLifecycleProbeEvent>(probe.Handle);

            var expected = Enumerable.Range(0, 40).OrderBy(i => i % 3).ToList();
            Assert.Equal(expected, log);
        }

        [Fact]
        public void RemoveOwned_DropsHandlersOfTheDyingAssembly()
        {
            var modId = TestMods.NewId("test_bus");
            var mod = TestMods.Compile(modId, ModSource);
            var handlers = mod.GetType("BusMod.Handlers");
            var handler = (Action<CharactersDefeatedEvent>)Delegate.CreateDelegate(typeof(Action<CharactersDefeatedEvent>), handlers.GetMethod("OnDefeated"));
            var hits = handlers.GetField("Hits", BindingFlags.Public | BindingFlags.Static);
            try
            {
                ModEventBus.Subscribe(handler);
                ModEventBus.Publish(new CharactersDefeatedEvent { Source = "bus_test" });

                var removed = ModEventBus.RemoveOwned(new AssemblyOwnership(new[] { mod }));
                ModEventBus.Publish(new CharactersDefeatedEvent { Source = "bus_test" });

                Assert.Equal(1, removed);
                Assert.Equal(1, (int)hits.GetValue(null));
            }
            finally
            {
                ModEventBus.Unsubscribe(handler);
                ModAssemblyLoader.UnloadMod(modId);
            }
        }

        [Fact]
        public void RemoveOwned_EmptyOwnership_RemovesNothing()
        {
            Assert.Equal(0, ModEventBus.RemoveOwned(new AssemblyOwnership(null)));
            Assert.Equal(0, ModEventBus.RemoveOwned(null));
        }
    }
}
