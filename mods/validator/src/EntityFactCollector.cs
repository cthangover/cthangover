using System.Text.Json;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Shared extraction helpers for <see cref="IEntityKindStrategy"/>
    /// implementations. Keeps the per-kind strategies focused on "what is
    /// interesting about my entities" while the mechanical work — walking
    /// declared object keys, action property keys, loot and ingredient
    /// references — lives in one place. All helpers are schema-light
    /// (case-insensitive raw <see cref="JsonElement"/> reads) so they survive
    /// DTO field changes.
    /// </summary>
    public static class EntityFactCollector
    {
        /// <summary>Declares every top-level property name of an entity as a known key.</summary>
        public static void CollectObjectKeys(ValidationGraph graph, JsonElement entity)
        {
            if (entity.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in entity.EnumerateObject())
                graph.AddDeclaredKey(property.Name);
        }

        /// <summary>Declares the <c>Key=Value</c> property bag keys of an action's <c>Values</c> array.</summary>
        public static void CollectActionKeys(ValidationGraph graph, JsonElement values)
        {
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                    continue;

                var text = value.GetString();
                if (string.IsNullOrEmpty(text))
                    continue;

                var separator = text.IndexOf('=');
                if (separator > 0)
                    graph.AddDeclaredKey(text.Substring(0, separator));
            }
        }

        /// <summary>Records item references from a recipe's ingredient array (e.g. <c>Input</c>, <c>Output</c>).</summary>
        public static void CollectIngredientReferences(ValidationContext context, IModInfo mod, string file, JsonElement entity, string field)
        {
            var ingredients = JsonReadHelper.GetProperty(entity, field);
            if (!JsonReadHelper.IsArray(ingredients))
                return;

            foreach (var ingredient in ingredients.Value.EnumerateArray())
            {
                var itemId = JsonReadHelper.GetString(ingredient, "Id", "ID", "id");
                if (!string.IsNullOrWhiteSpace(itemId))
                    context.Graph.AddReference(ValidationKind.Item, itemId, mod.Id, file, 0);
            }
        }

        /// <summary>Records item references from a character's <c>Loot</c> table.</summary>
        public static void CollectLootReferences(ValidationContext context, IModInfo mod, string file, JsonElement entity)
        {
            var loot = JsonReadHelper.GetProperty(entity, "Loot");
            if (!JsonReadHelper.IsArray(loot))
                return;

            foreach (var drop in loot.Value.EnumerateArray())
            {
                var itemId = JsonReadHelper.GetString(drop, "ItemId");
                if (!string.IsNullOrWhiteSpace(itemId))
                    context.Graph.AddReference(ValidationKind.Item, itemId, mod.Id, file, 0);
            }
        }

        /// <summary>Records action references from a character's <c>Actions</c> array.</summary>
        public static void CollectActionReferences(ValidationContext context, IModInfo mod, string file, JsonElement entity)
        {
            var actions = JsonReadHelper.GetProperty(entity, "Actions");
            if (!JsonReadHelper.IsArray(actions))
                return;

            foreach (var action in actions.Value.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.String)
                    continue;

                var actionId = action.GetString();
                if (!string.IsNullOrWhiteSpace(actionId))
                    context.Graph.AddReference(ValidationKind.Action, actionId, mod.Id, file, 0);
            }
        }
    }
}
