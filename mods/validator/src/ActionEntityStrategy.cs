using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects action declarations, their declared object
    /// keys and the property-bag keys from their <c>Values</c> list. Actions used
    /// to be distinguished from characters by a heuristic on the shared
    /// <c>characters/</c> directory; the kind makes that explicit.
    /// </summary>
    public class ActionEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Action;

        protected override void CollectEntity(ValidationContext context, IModInfo mod, string file, string id, JsonElement item)
        {
            base.CollectEntity(context, mod, file, id, item);
            EntityFactCollector.CollectObjectKeys(context.Graph, item);

            var values = JsonReadHelper.GetProperty(item, "Values");
            if (JsonReadHelper.IsArray(values))
                EntityFactCollector.CollectActionKeys(context.Graph, values.Value);
        }
    }
}
