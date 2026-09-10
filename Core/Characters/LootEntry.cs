using System.Text.Json.Serialization;
using Cthangover.Core.Mods.Patches;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Loot drop definition for enemies: ItemId, min/max count range, and
    /// probability (0–100 percentage). Implements
    /// <see cref="IMergeable"/> with <c>ItemId</c> as the identity key so
    /// that <c>Loot$add</c> and <c>Loot$remove</c> patches can match
    /// entries by item rather than by exact JSON text.
    /// </summary>
    public class LootEntry : IMergeable
    {
        string IMergeable.MergeKeyProperty => nameof(ItemId);

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
