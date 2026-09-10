using System.Collections.Generic;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Persistent collection of action IDs available in the action pool.
    /// These are actions owned by the player that can be assigned to character
    /// slots via drag-and-drop in the character panel. Initially empty —
    /// template actions start pre-assigned to character slots. External sources
    /// (scenarios, collectible cards) add actions here. Persisted to save data.
    /// </summary>
    public class ActionPoolData
    {
        public List<string> ActionIds { get; set; } = new();
    }
}
