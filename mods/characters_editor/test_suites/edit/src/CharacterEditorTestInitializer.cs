using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Tools.CharactersEditor.Tests
{
    /// <summary>Adds the editor test driver when <c>--itest=characters_editor_edit</c> is given.</summary>
    public class CharacterEditorTestInitializer : IModInitializer
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
            if (driverQueued || !args.Any(a => a == "--itest=characters_editor_edit"))
                return;

            driverQueued = true;
            var shots = args.Where(a => a.StartsWith("--shots=", System.StringComparison.Ordinal))
                .Select(a => a.Substring("--shots=".Length))
                .FirstOrDefault();
            GameLogger.Log("TEST", $"CharacterEditorTestInitializer: activating characters_editor_edit, shots={shots ?? "off"}");
            ModRoot.AddDeferred(new CharacterEditorTestDriver(shots));
        }
    }
}
