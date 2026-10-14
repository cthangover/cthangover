using Cthangover.Core.Events;
using Cthangover.Core.Interactive;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Interactive
{
    /// <summary>
    /// Engine-mod initializer for the interactive subsystem. Takes over the
    /// lifecycle <c>SceneContextNode</c> owned before the migration: on
    /// resources-ready it initializes the <c>InteractiveManager</c> singleton
    /// and subscribes the per-scene teardown (<c>InteractiveManager.Shutdown</c>)
    /// to <c>SceneExitedEvent</c>, so tracked interactive objects are destroyed
    /// on every scene change. All operations are idempotent:
    /// <c>ModManager.Reload()</c> re-runs <c>OnModResourcesReady</c>, so repeat
    /// invocations must not corrupt state.
    /// </summary>
    public class InteractiveModInitializer : IModInitializer
    {
        private static bool initialized;

        /// <summary>No per-mod setup is required; lifecycle happens on resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Initializes the manager singleton and wires the scene-exit teardown.
        /// Safe to call again after a mod reload.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (initialized)
                return;
            initialized = true;

            InteractiveManager.Initialize();
            GameLogger.Log("INTERACTIVE", "InteractiveModInitializer: manager initialized");

            ModEventBus.Subscribe<SceneExitedEvent>(_ => InteractiveManager.Shutdown());
        }
    }
}
