using System.Collections.Generic;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Which game wrote a save and with what composition. The id ties the
    /// save to the game (it lives in <c>user://saves/&lt;id&gt;/</c>); the mod
    /// list is a snapshot for comparing compositions on load, never a
    /// requirement — the game may have changed its mechanics since.
    /// </summary>
    public class SaveGameInfo
    {
        /// <summary>Game collection id (stable across the player's edits).</summary>
        public string Id { get; set; }

        /// <summary>Collection display name at save time.</summary>
        public string Name { get; set; }

        /// <summary>Collection version at save time.</summary>
        public string Version { get; set; }

        /// <summary>Mods the game ran with, in load order.</summary>
        public List<SaveGameMod> Mods { get; set; } = new();
    }

    /// <summary>One mod of a <see cref="SaveGameInfo"/> composition snapshot.</summary>
    public class SaveGameMod
    {
        public string Id { get; set; }

        public string Version { get; set; }
    }
}
