namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Represents the rarity tier of a skill card, determining its drop probability,
    /// visual frame treatment, and relative power budget. Higher tiers correspond to
    /// more potent effects and rarer acquisition chances.
    /// </summary>
    public enum RareType
    {
        /// <summary>Standard skill with the highest drop rate and baseline power.</summary>
        Common,
        /// <summary>Slightly improved stats and a lower drop rate than <c>Common</c>.</summary>
        Uncommon,
        /// <summary>Notably stronger, appearing infrequently in loot pools.</summary>
        Rare,
        /// <summary>Powerful skill with significant gameplay impact; very low drop chance.</summary>
        Epic,
        /// <summary>The highest tier — game-changing abilities with extremely rare availability.</summary>
        Legendary
    }
}
