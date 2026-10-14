using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Shared presentation rules for inventory items, used by both the grid
    /// cell and the item passport so the two can never disagree about an
    /// item's affordances. <see cref="ItemType"/> is a flags enum, so every
    /// question ("can this be used?", "what badge colour?") has to pick a
    /// priority among possibly overlapping flags — the order in
    /// <see cref="BadgeOrder"/> defines that priority consistently.
    /// A custom verb can be supplied by mods through the item's
    /// <c>Properties</c> bag under the <c>verb</c> key; it is treated as a
    /// locale key when it contains a slash and as a literal otherwise.
    /// </summary>
    public static class InventoryItemUi
    {
        /// <summary>Badge order from most narrative to most technical flag.</summary>
        public static readonly IReadOnlyList<ItemType> BadgeOrder = new[]
        {
            ItemType.Quest,
            ItemType.Used,
            ItemType.TargetUsed,
            ItemType.Food,
            ItemType.Resource,
            ItemType.Recipe,
            ItemType.CantDrop,
        };

        /// <summary>
        /// Mirrors <c>InventoryPanelBehaviour</c> use-button rule: an item is
        /// usable only when it carries an action and one of the "used" flags.
        /// Food or resources without an <see cref="IItemAction"/> are inert.
        /// </summary>
        public static bool IsUsable(IItem item)
        {
            if (item == null || item.ItemAction == null)
                return false;
            return item.ItemType.HasFlag(ItemType.Used) || item.ItemType.HasFlag(ItemType.TargetUsed);
        }

        /// <summary>False only for <see cref="ItemType.CantDrop"/> items (quest objectives, key items).</summary>
        public static bool CanDrop(IItem item)
        {
            return item != null && !item.ItemType.HasFlag(ItemType.CantDrop);
        }

        /// <summary>
        /// Locale key (or literal) of the verb shown on the action button.
        /// Resolution order: mod-provided <c>verb</c> property, recipe,
        /// targeted, food, generic use.
        /// </summary>
        public static string VerbKey(IItem item)
        {
            if (item == null)
                return "ui/inventory/verb_use";

            var custom = item.Properties?.GetStr("verb");
            if (!string.IsNullOrWhiteSpace(custom))
                return custom;

            if (item.ItemType.HasFlag(ItemType.Recipe))
                return "ui/inventory/verb_recipe";
            if (item.ItemType.HasFlag(ItemType.TargetUsed))
                return "ui/inventory/verb_target";
            if (item.ItemType.HasFlag(ItemType.Food))
                return "ui/inventory/verb_eat";
            return "ui/inventory/verb_use";
        }

        /// <summary>
        /// Short corner tag for the grid cell. Only actionable items get a
        /// tag — an inert resource must not advertise a verb it cannot run.
        /// </summary>
        public static string TagKey(IItem item)
        {
            if (item?.ItemAction == null)
                return null;

            if (item.ItemType.HasFlag(ItemType.Recipe))
                return "ui/inventory/tag_recipe";
            if (item.ItemType.HasFlag(ItemType.TargetUsed))
                return "ui/inventory/tag_target";
            if (item.ItemType.HasFlag(ItemType.Food))
                return "ui/inventory/tag_food";
            return "ui/inventory/tag_use";
        }

        /// <summary>
        /// Accent colour of an item: quest gold dominates, then recipe,
        /// food, targeted, used and finally plain resources.
        /// </summary>
        public static Color AccentColor(ItemType type)
        {
            if (type.HasFlag(ItemType.Quest))
                return UiTokens.TypeQuest;
            if (type.HasFlag(ItemType.Recipe))
                return UiTokens.TypeRecipe;
            if (type.HasFlag(ItemType.Food))
                return UiTokens.TypeFood;
            if (type.HasFlag(ItemType.TargetUsed))
                return UiTokens.TypeTarget;
            if (type.HasFlag(ItemType.Used))
                return UiTokens.TypeUsed;
            if (type.HasFlag(ItemType.Resource))
                return UiTokens.TypeResource;
            return UiTokens.Muted;
        }

        /// <summary>Locale key of the badge caption for a single type flag.</summary>
        public static string TypeBadgeKey(ItemType type)
        {
            if (type == ItemType.Quest)
                return "ui/inventory/type_quest";
            if (type == ItemType.Used)
                return "ui/inventory/type_used";
            if (type == ItemType.TargetUsed)
                return "ui/inventory/type_target";
            if (type == ItemType.Food)
                return "ui/inventory/type_food";
            if (type == ItemType.Resource)
                return "ui/inventory/type_resource";
            if (type == ItemType.Recipe)
                return "ui/inventory/type_recipe";
            if (type == ItemType.CantDrop)
                return "ui/inventory/type_cant_drop";
            return null;
        }

        /// <summary>Translates a locale key, falling back to the key itself for unknown keys and literals.</summary>
        public static string Translate(string keyOrLiteral)
        {
            if (string.IsNullOrEmpty(keyOrLiteral))
                return string.Empty;
            return TranslationServer.Translate(keyOrLiteral).ToString();
        }
    }
}
