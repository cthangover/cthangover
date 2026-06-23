using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    public class FFItemExecutor : IActionExecutor
    {
        public string ActionId => "ff/item";

        public ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            return new ChangedAttributes { Result = false };
        }

        public static bool TryUseItem(IItem item, Character user, Character target)
        {
            if (item == null || user == null)
                return false;

            if (user.Attributes.Point.Value < 1)
            {
                GameLogger.Log("FF_BATTLE", $"Not enough points to use item {item.Name}", LogLevel.Warning);
                return false;
            }

            var inventory = GameData.Instance.Runtime.Inventory;
            if (!inventory.HasItem(item.ID))
            {
                GameLogger.Log("FF_BATTLE", $"No {item.ID} in inventory", LogLevel.Warning);
                return false;
            }

            user.Attributes.Point.Value -= 1;
            inventory.Remove(item.ID, 1);

            if (item.ItemAction != null)
                item.ItemAction.UseAction(item);

            GameLogger.Log("FF_BATTLE", $"{user.Name} uses item {item.Name}", LogLevel.Debug);
            return true;
        }
    }
}
