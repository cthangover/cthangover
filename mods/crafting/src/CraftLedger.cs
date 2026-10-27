namespace Cthangover.Mods.Crafting
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Items;

    /// <summary>
    /// Craft arithmetic against raw recipe definitions. RecipeFactory tolerates unknown
    /// ingredients by dropping them, which is useful for display but unsafe for consumption.
    /// Here malformed entries, duplicate totals, overflow and every product are checked
    /// before the first mutation. Duplicate ingredient rows count together.
    /// </summary>
    public static class CraftLedger
    {
        /// <summary>Checks a complete batch without changing inventory; invalid data refuses the whole craft.</summary>
        public static bool CanCraft(IInventory inventory, RecipeInfo recipe, int count = 1)
        {
            if (inventory == null || recipe == null || count < 1 || recipe.Time < 0 || (long)recipe.Time * count > int.MaxValue)
                return false;
            var inputs = Totals(recipe.Input, count);
            var outputs = Totals(recipe.Output, count);
            if (inputs == null || outputs == null)
                return false;
            foreach (var pair in inputs)
                if (inventory.CheckCount(pair.Key) < pair.Value)
                    return false;
            foreach (var pair in outputs)
            {
                var consumed = inputs.TryGetValue(pair.Key, out var amount) ? amount : 0;
                if ((long)inventory.CheckCount(pair.Key) - consumed + pair.Value > int.MaxValue)
                    return false;
            }
            return true;
        }

        /// <summary>Consumes and produces one validated batch on the game thread; refusal leaves every stack untouched.</summary>
        public static bool TryCraft(IInventory inventory, RecipeInfo recipe, int count = 1)
        {
            if (!CanCraft(inventory, recipe, count))
                return false;
            var inputs = Totals(recipe.Input, count);
            var outputs = Totals(recipe.Output, count);
            foreach (var pair in inputs)
                inventory.Remove(pair.Key, pair.Value);
            foreach (var pair in outputs)
                inventory.Add(pair.Key, pair.Value);
            return true;
        }

        private static Dictionary<string, int> Totals(List<IngredientInfo> entries, int count)
        {
            if (entries == null || entries.Count == 0)
                return null;
            var definitions = Entities.Collect<ItemInfo>();
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || entry.Count <= 0 || string.IsNullOrWhiteSpace(entry.ID) || !definitions.ContainsKey(entry.ID))
                    return null;
                totals.TryGetValue(entry.ID, out var previous);
                var total = previous + (long)entry.Count * count;
                if (total > int.MaxValue)
                    return null;
                totals[entry.ID] = (int)total;
            }
            return totals;
        }
    }
}
