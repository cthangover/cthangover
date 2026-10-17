using System;

namespace Cthangover.Core.Settings
{
    /// <summary>One game with saves on disk, for the save browser's game selector.</summary>
    public class SaveGameSummary
    {
        /// <summary>Game collection id; null for saves made without a game.</summary>
        public string GameId { get; set; }

        /// <summary>Name recorded in the newest save.</summary>
        public string GameName { get; set; }

        public int SaveCount { get; set; }

        public DateTime LastSave { get; set; }

        /// <summary>Slot name of the newest save ("Continue" of that game).</summary>
        public string LastFileName { get; set; }
    }
}
