using System;
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

        /// <summary>
        /// How many items this entry drops on one roll: 0 when the
        /// <see cref="Probability"/> (percent, clamped to 0–100) misses,
        /// otherwise a count between <see cref="MinCount"/> and
        /// <see cref="MaxCount"/> inclusive. Swapped bounds are read in either
        /// order and negative counts drop nothing — a slip in a loot table
        /// must not throw on the victory screen, which rolls every entry.
        /// </summary>
        public int Roll(Random random)
        {
            var probability = Math.Clamp(Probability, 0, 100);
            if (probability == 0 || (probability < 100 && random.Next(1, 101) > probability))
                return 0;

            var low = Math.Max(0, Math.Min(MinCount, MaxCount));
            var high = Math.Max(0, Math.Max(MinCount, MaxCount));
            return random.Next(low, high + 1);
        }
    }
}
