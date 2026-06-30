using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Handles item usage during battle. Implements <see cref="IActionExecutor"/>
    /// but its <see cref="Execute"/> method is a no-op — actual item logic runs
    /// via <see cref="TryUseItem"/> which is called directly from
    /// <see cref="FFItemAnimation"/>. This separation exists because items carry
    /// their own <see cref="IItem.ItemAction"/> delegate for effects, unlike
    /// standard actions which use the executor pattern.
    /// Registered under ID <c>"ff/item"</c>.
    /// </summary>
    public class FFItemExecutor : IActionExecutor
    {
        /// <summary>The action ID string that maps to this executor in <see cref="FFActionProvider"/>.</summary>
        public string ActionId => "ff/item";

        /// <summary>No-op — item effects are applied via <see cref="TryUseItem"/>.</summary>
        public ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            return new ChangedAttributes { Result = false };
        }

        /// <summary>
        /// Consumes one action point from <paramref name="user"/>, removes the item
        /// from the global inventory, and invokes the item's
        /// <see cref="IItem.ItemAction"/>. Returns <c>false</c> if the user lacks
        /// points, the item is not in inventory, or preconditions fail.
        /// </summary>
        /// <param name="item">The item to use.</param>
        /// <param name="user">The character consuming the item (loses 1 point).</param>
        /// <param name="target">The character targeted by the item effect.</param>
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
