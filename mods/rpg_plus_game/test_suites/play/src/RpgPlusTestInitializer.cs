namespace Cthangover.Mods.RpgPlus.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Mounts <see cref="RpgPlusTestDriver"/> when the game runs with <c>--itest=rpg_plus</c>.</summary>
    public class RpgPlusTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (driverQueued || !args.Any(a => a == "--itest=rpg_plus"))
                return;
            driverQueued = true;
            var shots = args.Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();
            GameLogger.Log("TEST", $"RpgPlusTestInitializer: activating rpg_plus, shots={shots ?? "off"}");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new RpgPlusTestDriver(shots));
        }
    }
}
