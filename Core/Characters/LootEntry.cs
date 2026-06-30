using System.Text.Json.Serialization;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Loot drop definition for enemies: ItemId, min/max count range, and
    /// probability (presumably 0–100 percentage). Uses JsonPropertyName
    /// attributes for PascalCase JSON keys matching Godot's C# serialization
    /// conventions. The count range allows variable drops (e.g. "1–3 potions").
    /// </summary>
    public class LootEntry
    {
        /// <summary>
        /// Item identifier string for the loot drop. Resolved against
        /// the item registry at drop time.
        /// </summary>
        [JsonPropertyName("ItemId")]
        public string ItemId { get; set; }

        /// <summary>
        /// Minimum quantity of this item to drop when the roll succeeds.
        /// </summary>
        [JsonPropertyName("MinCount")]
        public int MinCount { get; set; }

        /// <summary>
        /// Maximum quantity of this item to drop when the roll succeeds.
        /// Together with <see cref="MinCount"/> defines an inclusive
        /// range for randomized drop amounts.
        /// </summary>
        [JsonPropertyName("MaxCount")]
        public int MaxCount { get; set; }

        /// <summary>
        /// Drop probability as a percentage (0–100). Evaluated by the
        /// loot system after battle to determine whether this entry
        /// produces a drop.
        /// </summary>
        [JsonPropertyName("Probability")]
        public int Probability { get; set; }
    }

}
