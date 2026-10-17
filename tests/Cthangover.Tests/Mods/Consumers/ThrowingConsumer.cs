namespace Cthangover.Tests.Mods.Consumers
{
    using System;
    using System.Reflection;
    using Cthangover.Core.Mods.Consumers;

    /// <summary>A consumer whose load pass fails, for the failure-isolation tests.</summary>
    internal sealed class ThrowingConsumer : IAssemblyConsumer
    {
        public string Id => "tests.throwing";

        public int Order => 10;

        public void OnAssemblyLoaded(Assembly assembly, string modId) => throw new InvalidOperationException("load failure");

        public void OnAssemblyUnloading(Assembly assembly, string modId)
        {
        }
    }
}
