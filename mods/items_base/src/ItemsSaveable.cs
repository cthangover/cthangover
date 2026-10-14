using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// Persists the player inventory and unlocked recipes in the items mod's
    /// own save slot. The core used to carry the <c>"Inventory"</c> and
    /// <c>"Recipes"</c> blobs inside the <c>__core__</c> slot; the blob
    /// format (CItem id/count pairs, recipe id list) is unchanged — only the
    /// owning slot moved. Backward compatibility with old saves' core blobs
    /// is intentionally not provided (precedent: recruiting) — old saves
    /// start with an empty inventory and no unlocked recipes.
    /// </summary>
    public class ItemsSaveable : IModSaveable
    {
        private const string InventoryBlob = "Inventory";
        private const string RecipesBlob = "Recipes";

        /// <summary>Writes item stacks and unlocked recipe IDs into the mod slot.</summary>
        public void OnSave(ModSlot slot)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
                return;

            slot.Data[InventoryBlob] = new DataBlob(1, runtime.Inventory.Items.Select(o =>
                new CItem { ID = o.Item.ID, Count = o.Count }).ToList());
            slot.Data[RecipesBlob] = new DataBlob(1, runtime.RecipeData.Data.ToList());
        }

        /// <summary>Restores inventory and recipes; absent blobs leave the current state untouched.</summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null || slot?.Data == null)
                return true;

            if (slot.Data.TryGetValue(InventoryBlob, out var invBlob))
            {
                var items = invBlob.ReadJson<List<CItem>>();
                runtime.Inventory.Items = items?.Select(o =>
                    (IItemContainer)new ItemContainer
                        { Item = ItemFactory.Instance.Get(o.ID), Count = o.Count }).ToList()
                    ?? new List<IItemContainer>();
            }

            if (slot.Data.TryGetValue(RecipesBlob, out var recipeBlob))
                runtime.RecipeData.Data = recipeBlob.ReadJson<List<string>>()?.ToHashSet()
                    ?? new HashSet<string>();

            return true;
        }
    }
}
