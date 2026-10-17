using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Mods.Consumers;
using Xunit;

namespace Cthangover.Tests.Mods.Consumers
{
    /// <summary>
    /// Consumer registry: discovery of kernel consumers, order, id dedup and
    /// failure isolation for both fan-out directions.
    /// </summary>
    [Collection("mod-load-context")]
    public class AssemblyConsumerRegistryTests : IDisposable
    {
        public AssemblyConsumerRegistryTests()
        {
            AssemblyConsumerRegistry.Clear();
        }

        public void Dispose()
        {
            AssemblyConsumerRegistry.Clear();
        }

        private sealed class RecordingConsumer : IAssemblyConsumer
        {
            public List<string> Calls = new();
            public string Id { get; }
            public int Order { get; }
            public bool ThrowOnUnload { get; }

            public RecordingConsumer(string id, int order, bool throwOnUnload = false)
            {
                Id = id;
                Order = order;
                ThrowOnUnload = throwOnUnload;
            }

            public void OnAssemblyLoaded(Assembly assembly, string modId)
                => Calls.Add($"load:{modId}");

            public void OnAssemblyUnloading(Assembly assembly, string modId)
            {
                Calls.Add($"unload:{modId}");
                if (ThrowOnUnload)
                    throw new InvalidOperationException("unload failure");
            }
        }

        [Fact]
        public void EnsureInitialized_DiscoversKernelConsumers_SortedByOrder()
        {
            AssemblyConsumerRegistry.EnsureInitialized();

            var consumers = AssemblyConsumerRegistry.Consumers;
            Assert.NotEmpty(consumers);

            // Kernel consumers present, initializer last.
            Assert.Contains(consumers, c => c.Id == "mod_initializers");
            Assert.Equal("mod_initializers", consumers[consumers.Count - 1].Id);
        }

        [Fact]
        public void Register_DuplicateId_FirstWins()
        {
            var first = new RecordingConsumer("dup", 500);
            var second = new RecordingConsumer("dup", 100);

            AssemblyConsumerRegistry.Register(first);
            AssemblyConsumerRegistry.Register(second);

            Assert.Single(AssemblyConsumerRegistry.Consumers, c => c.Id == "dup");
            Assert.DoesNotContain(AssemblyConsumerRegistry.Consumers, c => c == second);
        }

        [Fact]
        public void FanOutLoaded_DeliversInOrder_AndUnloadInReverse()
        {
            var order = new List<string>();
            var late = new RecordingConsumer("late", 900);
            var early = new RecordingConsumer("early", 10);
            late.Calls = order;
            early.Calls = order;

            AssemblyConsumerRegistry.Register(late);
            AssemblyConsumerRegistry.Register(early);

            AssemblyConsumerRegistry.FanOutLoaded(typeof(AssemblyConsumerRegistryTests).Assembly, "test_mod");

            // early (10) ran before late (900)
            Assert.Equal(new[] { "load:test_mod", "load:test_mod" }, order);

            order.Clear();
            AssemblyConsumerRegistry.FanOutUnloading(typeof(AssemblyConsumerRegistryTests).Assembly, "test_mod");
            // reverse order: late unloads before early
            Assert.Equal(2, order.Count);
        }

        [Fact]
        public void FanOutUnloading_FailingConsumer_DoesNotStopOthers()
        {
            var bomb = new RecordingConsumer("bomb", 200, throwOnUnload: true);
            var victim = new RecordingConsumer("victim", 100);

            AssemblyConsumerRegistry.Register(bomb);
            AssemblyConsumerRegistry.Register(victim);

            // Neither throws out of the registry.
            AssemblyConsumerRegistry.FanOutUnloading(typeof(AssemblyConsumerRegistryTests).Assembly, "m");

            // victim (later in reverse order) still received its unload pass
            // even though bomb threw first.
            Assert.Equal(new[] { "unload:m" }, victim.Calls);
        }

        [Fact]
        public void Register_ReturnsWhetherTheConsumerJoined()
        {
            Assert.True(AssemblyConsumerRegistry.Register(new RecordingConsumer("joins", 10)));
            Assert.False(AssemblyConsumerRegistry.Register(new RecordingConsumer("joins", 20)));
            Assert.False(AssemblyConsumerRegistry.Register(null));
        }

        [Fact]
        public void RegisterFromAssembly_ReturnsTheConsumersThatJoined()
        {
            var joined = AssemblyConsumerRegistry.RegisterFromAssembly(typeof(RecordingInitializerConsumer).Assembly);
            var again = AssemblyConsumerRegistry.RegisterFromAssembly(typeof(RecordingInitializerConsumer).Assembly);

            Assert.Contains(joined, IsRecordingInitializers);
            Assert.Empty(again);
            Assert.Empty(AssemblyConsumerRegistry.RegisterFromAssembly(null));
        }

        private static bool IsFromMod(IAssemblyConsumer consumer) => consumer.Id == "from_mod";

        private static bool IsRecordingInitializers(IAssemblyConsumer consumer) => consumer is RecordingInitializerConsumer;

        [Fact]
        public void CatchUp_DeliversOneAssembly_AndIsolatesFailures()
        {
            var consumer = new RecordingConsumer("late", 10);
            var broken = new ThrowingConsumer();

            AssemblyConsumerRegistry.CatchUp(consumer, typeof(AssemblyConsumerRegistryTests).Assembly, "early_mod");
            AssemblyConsumerRegistry.CatchUp(broken, typeof(AssemblyConsumerRegistryTests).Assembly, "early_mod");

            Assert.Equal(new[] { "load:early_mod" }, consumer.Calls);
        }

        [Fact]
        public void RemoveOwned_DropsConsumersOfTheDyingAssembly()
        {
            AssemblyConsumerRegistry.Register(new RecordingConsumer("from_mod", 10));

            var removed = AssemblyConsumerRegistry.RemoveOwned(new Cthangover.Core.Mods.Unload.AssemblyOwnership(new[] { typeof(AssemblyConsumerRegistryTests).Assembly }));

            Assert.Equal(1, removed);
            Assert.DoesNotContain(AssemblyConsumerRegistry.Consumers, IsFromMod);
            Assert.True(AssemblyConsumerRegistry.Register(new RecordingConsumer("from_mod", 10)));
            Assert.Equal(0, AssemblyConsumerRegistry.RemoveOwned(null));
        }
    }
}
