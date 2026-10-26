using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.ModManagerHelpTest
{
    /// <summary>Queues <see cref="ModHelpTestDriverNode"/> only for <c>--itest=mod_help</c>.</summary>
    public class ModHelpTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=mod_help"))
                return;

            queued = true;
            GameLogger.Log("TEST", "ModHelpTest: activating");
            ModRoot.AddDeferred(new ModHelpTestDriverNode());
        }
    }
}
