using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects character declarations (player characters and
    /// enemies alike), their declared keys, loot item references and combat
    /// action references. This is the richest entity kind; the other kinds only
    /// look at reference subsets.
    /// </summary>
    public class CharacterEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Character;

        protected override void CollectEntity(ValidationContext context, IModInfo mod, string file, string id, JsonElement item)
        {
            base.CollectEntity(context, mod, file, id, item);
            EntityFactCollector.CollectObjectKeys(context.Graph, item);
            EntityFactCollector.CollectLootReferences(context, mod, file, item);
            EntityFactCollector.CollectActionReferences(context, mod, file, item);
        }
    }
}
