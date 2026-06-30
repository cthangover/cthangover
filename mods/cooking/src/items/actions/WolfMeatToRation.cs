using Cthangover.Core.Settings;

namespace Cthangover.Core.Items.Actions
{
    /// <summary>
    /// Performs the "Wolf Meat to Ration" recipe-unlock action.
    /// When a player uses raw wolf meat, this action checks whether the
    /// <c>recipe/wolf_meat_to_ration</c> flag is already present in
    /// <see cref="RecipeData"/>; if not, it adds it, permanently unlocking
    /// the recipe in the <see cref="Mods.Cooking.Workbench.WorkbenchPanel"/>.
    /// Returns <c>false</c> when already unlocked (no-op) and <c>true</c>
    /// on first unlock.
    /// </summary>
    public class WolfMeatToRation : IItemAction
    {
        /// <summary>
        /// Globally unique action identifier, matched by the item system
        /// to trigger this action when an item with this ID is used.
        /// </summary>
        public string ID => "action/recipe/wolf_meat_to_ration";
        
        /// <summary>
        /// Idempotently unlocks the wolf-meat-to-ration recipe.
        /// Adds <c>recipe/wolf_meat_to_ration</c> to <see cref="RecipeData"/>
        /// so that the cooking workbench can list it.
        /// </summary>
        /// <returns><c>true</c> if the recipe was newly unlocked; <c>false</c> if already known.</returns>
        public bool UseAction(IItem item)
        {
            var data = GameData.Instance.Runtime.RecipeData;
            if (data.Has("recipe/wolf_meat_to_ration"))
                return false;
            data.Add("recipe/wolf_meat_to_ration");
            return true;
        }
    }
}
