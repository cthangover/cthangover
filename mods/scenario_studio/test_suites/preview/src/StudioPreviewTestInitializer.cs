namespace Cthangover.Mods.ScenarioStudio.Tests.Preview
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Entry point of the studio preview test, active only with
    /// <c>--itest=studio_preview</c>; <c>--shots=&lt;dir&gt;</c> also saves a
    /// screenshot of every visited state (needs a real display, e.g. xvfb).
    /// The driver is added deferred because resources-ready fires before the
    /// shell scene exists.
    /// </summary>
    public class StudioPreviewTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (driverQueued)
                return;

            var args = OS.GetCmdlineUserArgs();
            if (!args.Any(a => a == "--itest=studio_preview"))
                return;

            var shots = args
                .Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();

            driverQueued = true;
            GameLogger.Log("TEST", $"StudioPreviewTestInitializer: activating studio preview test, shots='{shots ?? ""}'");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new StudioPreviewDriverNode(shots));
        }
    }
}
