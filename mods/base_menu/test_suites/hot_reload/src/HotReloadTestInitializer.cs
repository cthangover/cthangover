using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.HotReloadTest
{
    /// <summary>Queues <see cref="HotReloadTestDriverNode"/> only for <c>--itest=hot_reload</c>.</summary>
    public class HotReloadTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains(HotReloadTestDriverNode.Flag))
                return;

            queued = true;
            GameLogger.Log("TEST", "HotReloadTest: activating");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new HotReloadTestDriverNode());
        }
    }
}
