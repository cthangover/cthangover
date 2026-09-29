using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects item declarations and their declared keys.
    /// Items are the base entity other kinds reference (loot, recipe
    /// ingredients), so their keys feed the unknown-property checks while the
    /// declarations feed the reference reconciliation.
    /// </summary>
    public class ItemEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Item;

        protected override void CollectEntity(ValidationContext context, IModInfo mod, string file, string id, JsonElement item)
        {
            base.CollectEntity(context, mod, file, id, item);
            EntityFactCollector.CollectObjectKeys(context.Graph, item);
        }
    }
}
