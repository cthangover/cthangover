using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.SaveDormancyTest
{
    /// <summary>Queues <see cref="SaveDormancyTestDriverNode"/> only for <c>--itest=save_dormancy</c>.</summary>
    public class SaveDormancyTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=save_dormancy"))
                return;

            queued = true;
            GameLogger.Log("TEST", "SaveDormancyTest: activating");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new SaveDormancyTestDriverNode());
        }
    }
}
