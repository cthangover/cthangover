using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.NewGameTest
{
    /// <summary>Queues <see cref="NewGameTestDriverNode"/> only for <c>--itest=new_game</c>.</summary>
    public class NewGameTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains(NewGameTestDriverNode.Flag))
                return;

            queued = true;
            GameLogger.Log("TEST", "NewGameTest: activating");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new NewGameTestDriverNode());
        }
    }
}
