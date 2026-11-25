namespace Cthangover.Mods.ExecutionBootstrap
{
    using Cthangover.Core.Mods;
    using Godot;

    /// <summary>Queues the default-runtime probe only when its inert suite is explicitly activated.</summary>
    public sealed class ExecutionBootstrapInitializer : IModInitializer
    {
        private static bool queued;

        /// <summary>Individual mod notifications do not start the probe before resources and boot are ready.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>The driver waits for completed boot; it never enables or substitutes a test execution context.</summary>
        public void OnModResourcesReady()
        {
            if (queued)
            {
                return;
            }

            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument == "--itest=test_execution_bootstrap")
                {
                    queued = true;
                    ModRoot.AddDeferred(new ExecutionBootstrapDriver());
                    return;
                }
            }
        }
    }
}
