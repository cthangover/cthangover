using System;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// Restores health of the chosen party member (<c>"ItemAction": "action/heal"</c>).
    /// The amount is item data, not code: the item's <c>"heal"</c> key (an extra JSON
    /// key, so it lands in <see cref="IItem.Properties"/>), which lets one action serve
    /// a mushroom and an elixir alike. Health never rises above its base value, and a
    /// fallen character (health 0) is not revived — that is a different effect.
    /// The party member's attributes are the very objects the battle uses, so a heal
    /// in the inventory during a battle shows on the battle widgets at once.
    /// </summary>
    public class HealItemAction : ITargetedItemAction
    {
        /// <summary>Item property with the amount of health restored.</summary>
        public const string HealProperty = "heal";

        /// <summary>Action id referenced by item definitions.</summary>
        public string ID => "action/heal";

        /// <summary>Healing always needs a target; without one nothing happens.</summary>
        public bool UseAction(IItem item) => false;

        /// <summary>
        /// Adds the item's <c>heal</c> amount to the target's health, capped at the
        /// base value. Returns <c>false</c> (item not spent) for a missing target, a
        /// non-positive amount, a fallen target or one already at full health.
        /// </summary>
        public bool UseOn(IItem item, CharacterInfoData target)
        {
            var health = target?.Attributes?.Health;
            var amount = item?.Properties?.GetInt(HealProperty) ?? 0;
            if (health == null || amount <= 0 || health.Value <= 0 || health.Value >= health.BaseValue)
                return false;

            health.Value = Math.Min(health.BaseValue, health.Value + amount);
            return true;
        }
    }
}
