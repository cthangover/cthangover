using System;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Metadata about a single save slot, constructed by
    /// <see cref="SaveService.GetSaveSlots"/> to populate the load-game UI.
    /// Contains both the header info read from the JSON file and derived
    /// flags such as <see cref="HasScreenshot"/> and <see cref="IsEmpty"/>.
    /// Even empty slots (no file on disk) produce an instance so the UI
    /// always has the full grid of slot entries.
    /// </summary>
    public class SaveSlotInfo
    {
        /// <summary>Logical slot name (e.g. "slot_1").</summary>
        public string FileName { get; set; }
        /// <summary>UTC timestamp of when the save was written.</summary>
        public DateTime SaveTime { get; set; }
        /// <summary>Name of the scene the player was in at save time.</summary>
        public string SceneName { get; set; }
        /// <summary>Godot resource path to the thumbnail PNG (e.g. "user://saves/slot_1.png").</summary>
        public string ScreenshotPath { get; set; }
        /// <summary><c>true</c> if a thumbnail PNG exists on disk for this slot.</summary>
        public bool HasScreenshot { get; set; }
        /// <summary><c>true</c> when no JSON save file exists for this slot.</summary>
        public bool IsEmpty { get; set; }
        /// <summary>In-game time in minutes (<see cref="TimeData.Tick"/>) at save moment.</summary>
        public long GameTime { get; set; }
        /// <summary>Number of recruited characters in the party.</summary>
        public int CharacterCount { get; set; }
        /// <summary>Normalised lamp power, used by the UI for a fill bar
        /// (calculated externally from <see cref="SaveData.LampRadius"/>).</summary>
        public float LampPercent { get; set; }
    }
}
