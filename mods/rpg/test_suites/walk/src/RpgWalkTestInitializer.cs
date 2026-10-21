using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>
    /// Entry point of the rpg_walk integration test: adds the driver to the tree
    /// root when the exact flag <c>--itest=rpg_walk</c> is given (the driver
    /// quits the process, so it must never run in another test). Optional
    /// <c>--shots=&lt;dir&gt;</c> makes the driver save screenshots.
    /// </summary>
    public class RpgWalkTestInitializer : IModInitializer
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
            if (driverQueued || !args.Any(a => a == "--itest=rpg_walk"))
                return;

            driverQueued = true;
            var shots = args.Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();
            GameLogger.Log("TEST", $"RpgWalkTestInitializer: activating rpg_walk, shots={shots ?? "off"}");
            (Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new RpgWalkTestDriver(shots));
        }
    }
}
