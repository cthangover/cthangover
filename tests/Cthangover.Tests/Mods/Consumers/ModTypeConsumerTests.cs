namespace Cthangover.Tests.Mods.Consumers
{
    using Cthangover.Core.Mods;
    using Cthangover.Tests.Mods.Unload;

    /// <summary>
    /// The typed consumer base a mod registry derives from: one instance per
    /// implementation of an assembly on load, the same instances back on unload.
    /// </summary>
    [Collection("mod-load-context")]
    public class ModTypeConsumerTests
    {
        private const string ModSource = @"
namespace ConsumerMod
{
    public class FirstInit : Cthangover.Core.Mods.IModInitializer
    {
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady() { }
    }

    public class SecondInit : Cthangover.Core.Mods.IModInitializer
    {
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady() { }
    }

    public abstract class NotCreated : Cthangover.Core.Mods.IModInitializer
    {
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady() { }
    }
}
";

        [Fact]
        public void LoadedThenUnloading_SameInstancesComeBack()
        {
            var modId = TestMods.NewId("test_consumer");
            var mod = TestMods.Compile(modId, ModSource);
            var consumer = new RecordingInitializerConsumer();
            try
            {
                consumer.OnAssemblyLoaded(mod, modId);
                consumer.OnAssemblyLoaded(mod, modId);

                Assert.Equal(2, consumer.Added.Count);
                foreach (var (_, addedFor) in consumer.Added)
                    Assert.Equal(modId, addedFor);

                consumer.OnAssemblyUnloading(mod, modId);

                Assert.Equal(consumer.Added, consumer.Removed);
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(modId);
            }
        }

        [Fact]
        public void Unloading_AnAssemblyNeverLoaded_RemovesNothing()
        {
            var consumer = new RecordingInitializerConsumer();

            consumer.OnAssemblyUnloading(typeof(object).Assembly, "core");

            Assert.Empty(consumer.Removed);
        }

        [Fact]
        public void Assembly_WithoutImplementations_AddsNothing()
        {
            var consumer = new RecordingInitializerConsumer();

            consumer.OnAssemblyLoaded(typeof(object).Assembly, "framework");

            Assert.Empty(consumer.Added);
            Assert.Equal(500, consumer.Order);
            Assert.Equal("tests.recording_initializers", consumer.Id);
        }
    }
}
