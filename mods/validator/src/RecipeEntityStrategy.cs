using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Collects recipe declarations and turns their
    /// <c>Input</c>/<c>Output</c> ingredients into item references, so a recipe
    /// pointing at a deleted item surfaces as a missing reference.
    /// </summary>
    public class RecipeEntityStrategy : EntityKindStrategy
    {
        public override string Kind => ValidationKind.Recipe;

        protected override void CollectEntity(ValidationContext context, IModInfo mod, string file, string id, JsonElement item)
        {
            base.CollectEntity(context, mod, file, id, item);
            EntityFactCollector.CollectIngredientReferences(context, mod, file, item, "Input");
            EntityFactCollector.CollectIngredientReferences(context, mod, file, item, "Output");
        }
    }
}
