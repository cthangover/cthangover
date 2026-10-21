using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Tools.RpgEditor.Tests
{
    /// <summary>Adds the editor test driver when <c>--itest=rpg_editor_edit</c> is given.</summary>
    public class RpgEditorTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues the driver once.</summary>
        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (driverQueued || !args.Any(a => a == "--itest=rpg_editor_edit"))
                return;

            driverQueued = true;
            var shots = args.Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();
            GameLogger.Log("TEST", $"RpgEditorTestInitializer: activating rpg_editor_edit, shots={shots ?? "off"}");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new RpgEditorTestDriver(shots));
        }
    }
}
