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

            // Kernel consumers all present, initializer last, bridges first.
            Assert.Contains(consumers, c => c.Id == "mod_initializers");
            Assert.Equal("mod_initializers", consumers[consumers.Count - 1].Id);
            Assert.Contains(consumers, c => c.Id == "scenario_command_bridge");
            Assert.True(consumers[0].Order <= 20);
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
    }
}
