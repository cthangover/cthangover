using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.MenuFlowTest
{
    /// <summary>Queues <see cref="MenuFlowTestDriverNode"/> only for <c>--itest=menu_flow</c>.</summary>
    public class MenuFlowTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=menu_flow"))
                return;

            queued = true;
            GameLogger.Log("TEST", "MenuFlowTest: activating");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new MenuFlowTestDriverNode());
        }
    }
}
