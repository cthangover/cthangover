namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;

    /// <summary>The saved stats of a game: per character id (the stats slot's one blob).</summary>
    public class StatStateData
    {
        /// <summary>Character id → what it gained during play.</summary>
        public Dictionary<string, CharacterStatState> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
