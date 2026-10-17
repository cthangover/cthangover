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
    /// Engine-mod initializer for the dialog subsystem. On resources-ready it
    /// publishes the scenario event factory used by the core SceneManager and
    /// parks the <c>/root/DialogRunner</c> node implementing the core
    /// <c>IInteractiveDialogRunner</c> contract. All operations are idempotent:
    /// <c>ModManager.Reload()</c> re-runs <c>OnModResourcesReady</c>, so node
    /// creation must tolerate repeat invocations. Scenario command strategies
    /// of every mod reach their factory through
    /// <see cref="ScenarioCommandStrategyConsumer"/>, not through this initializer.
    /// </summary>
    public class DialogModInitializer : IModInitializer
    {
        private static bool initialized;

        /// <summary>No per-mod setup is required; registration happens on resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Publishes the scenario event factory and queues the dialog runner
        /// node. Safe to call again after a mod reload.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (initialized)
                return;
            initialized = true;

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