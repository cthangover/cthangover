using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenarios;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Cthangover.Mods.Dialog.Scenarios;
using Godot;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Engine-mod initializer for the dialog subsystem. On resources-ready it:
    /// binds the core <c>ScenarioCommandBridge</c> to this mod's strategy
    /// factory (flushing every hot-loaded mod assembly buffered so far),
    /// publishes the scenario event factory used by the core SceneManager, and
    /// parks the <c>/root/DialogRunner</c> node implementing the core
    /// <c>IInteractiveDialogRunner</c> contract. All operations are idempotent:
    /// <c>ModManager.Reload()</c> re-runs <c>OnModResourcesReady</c>, so node
    /// creation and binding must tolerate repeat invocations.
    /// </summary>
    public class DialogModInitializer : IModInitializer
    {
        private static bool initialized;

        /// <summary>No per-mod setup is required; registration happens on resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Binds the strategy bridge, publishes the scenario event factory and
        /// queues the dialog runner node. Safe to call again after a mod reload.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (initialized)
                return;
            initialized = true;

            // Scenario command strategies: first-wins inside the factory, so a
            // reloaded dialog assembly keeps the strategy set stable.
            ScenarioCommandBridge.Bind(assembly => ScenarioCommandStrategyFactory.RegisterAssembly(assembly));

            // Scenario-backed events (scene definitions and test-play) are built
            // by the core SceneManager through this factory.
            AppServices.Current.SceneEvents.ScenarioEventFactory = new ScenarioEventFactory();

            EnsureDialogRunner();
        }

        private static void EnsureDialogRunner()
        {
            var root = ModEventBus.Instance?.GetTree()?.Root;
            if (root == null)
            {
                GameLogger.Log("DIALOG", "DialogModInitializer: tree root not available, skipping runner setup", LogLevel.Error);
                return;
            }

            if (root.GetNodeOrNull("DialogRunner") != null)
                return;

            // Deferred: OnModResourcesReady fires while the root is still
            // setting up autoload children, so a direct AddChild is rejected.
            root.CallDeferred("add_child", new DialogScriptRunner { Name = "DialogRunner" });
        }
    }
}