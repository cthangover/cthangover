using Cthangover.Core.Settings;

namespace Cthangover.Core.Items.Actions
{
    public class WolfMeatToRation : IItemAction
    {
        public string ID => "action/recipe/wolf_meat_to_ration";
        
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
