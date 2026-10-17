using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// Lifecycle entry point of the items mod. The core no longer owns the
    /// inventory/recipe state or the item factories, so this initializer:
    /// <list type="number">
    /// <item>installs the <see cref="RuntimeData"/> factory hooks, so the
    /// core state bag lazily produces the mod-owned <c>Inventory</c> and
    /// <c>RecipeData</c> on first access; and</item>
    /// <item>binds <see cref="ItemActionBridge"/> to this mod's
    /// <c>ItemActionFactory</c>, flushing every GDScript-declared action
    /// buffered so far.</item>
    /// </list>
    /// C# item actions of every mod reach the factory through
    /// <see cref="ItemActionConsumer"/>, not through this initializer.
    /// Persistence lives in <see cref="ItemsSaveable"/>, discovered
    /// automatically by <c>ModSaveableRegistry</c>.
    /// </summary>
    public class ItemsModInitializer : IModInitializer
    {
        /// <summary>No per-mod work is needed on load; discovery is reflection-based.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Installs the RuntimeData factory hooks and binds the item-action bridge (both idempotent).</summary>
        public void OnModResourcesReady()
        {
            RuntimeData.InventoryFactory ??= () => new Inventory();
            RuntimeData.RecipeDataFactory ??= () => new RecipeData();

            ItemActionBridge.Bind(action => ItemActionFactory.Instance.Register(action));
        }
    }
}
