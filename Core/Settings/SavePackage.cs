using System;
using System.Collections.Generic;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Root DTO of the save file. Contains save identification
    /// (SaveId, Timestamp, CoreVersion) and a <see cref="ModData"/>
    /// dictionary holding data slots from every mod that implements
    /// <see cref="Cthangover.Core.Mods.IModSaveable"/>, plus the
    /// reserved <c>"__core__"</c> slot that stores all core game state.
    ///
    /// The core itself does not inspect or deserialise mod blob
    /// payloads — it only routes byte arrays between the save file and
    /// the mod that owns each slot.
    /// </summary>
    public class SavePackage
    {
        public const string CoreSlotKey = "__core__";

        /// <summary>User-facing save name (e.g. "slot_1").</summary>
        public string SaveId { get; set; }

        /// <summary>UTC timestamp when the save was created.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Version of the core that created this save.</summary>
        public string CoreVersion { get; set; }

        /// <summary>
        /// Data slots from mods, keyed by ModId.
        /// The reserved key <c>"__core__"</c> holds core game state.
        /// </summary>
        public Dictionary<string, ModSlot> ModData { get; set; } = new();
    }
}
