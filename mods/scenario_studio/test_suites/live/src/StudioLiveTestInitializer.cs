namespace Cthangover.Mods.ScenarioStudio.Tests.Live
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Entry point of the studio live test, active only with
    /// <c>--itest=studio_live</c>; <c>--shots=&lt;dir&gt;</c> also saves a
    /// screenshot per checked state (needs a real display, e.g. xvfb). The
    /// test expects <c>--studio-write=layer</c> and its own
    /// <c>--studio-layer=</c> so it never writes into a mod. The driver is added
    /// deferred because resources-ready fires before the shell scene exists.
    /// </summary>
    public class StudioLiveTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (driverQueued)
                return;

            var args = OS.GetCmdlineUserArgs();
            if (!args.Any(a => a == "--itest=studio_live"))
                return;

            var shots = args
                .Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();

            driverQueued = true;
            GameLogger.Log("TEST", $"StudioLiveTestInitializer: activating studio live test, shots='{shots ?? ""}'");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new StudioLiveDriverNode(shots));
        }
    }
}
