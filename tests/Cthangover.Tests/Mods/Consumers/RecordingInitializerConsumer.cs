namespace Cthangover.Tests.Mods.Consumers
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Consumers;

    /// <summary>A mod-style typed registry for the consumer tests: records what the kernel hands it.</summary>
    public class RecordingInitializerConsumer : ModTypeConsumer<IModInitializer>
    {
        public readonly List<(IModInitializer Instance, string ModId)> Added = new();
        public readonly List<(IModInitializer Instance, string ModId)> Removed = new();

        public override string Id => "tests.recording_initializers";

        protected override void Add(IModInitializer instance, string modId) => Added.Add((instance, modId));

        protected override void Remove(IModInitializer instance, string modId) => Removed.Add((instance, modId));
    }
}
