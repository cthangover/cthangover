using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// JSON DTO for recipe definitions in mod files. Uses a plain
    /// <c>JsonStringEnumConverter</c> for <c>WorkbenchType</c> (simple
    /// enum, no flags needed) rather than the
    /// <c>FlagsStringEnumConverter</c> used by <c>ItemInfo</c>.
    /// <c>Input</c> and <c>Output</c> are raw <c>IngredientInfo</c>
    /// lists — the actual <c>IItem</c> references are resolved by
    /// <c>RecipeFactory</c> at cache population time, which also
    /// validates that every referenced item exists in <c>ItemFactory</c>.
    /// </summary>
    [Serializable]
    [EntityKind("recipe")]
    public class RecipeInfo : IIdentifiable
    {
        /// <summary>Unique recipe ID, serialized as "Id" in JSON.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        /// <summary>Locale key for the recipe display name.</summary>
        public string Name { get; set; }
        /// <summary>Locale key for the recipe description text.</summary>
        public string Description { get; set; }
        /// <summary>Required crafting station, serialized as its enum name ("Cooking").</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkbenchType Type { get; set; }
        /// <summary>Crafting time in in-game minutes.</summary>
        public int Time { get; set; }
        /// <summary>Raw ingredient entries; item references are resolved and validated by <see cref="Factories.Impls.RecipeFactory"/>.</summary>
        public List<IngredientInfo> Input { get; set; }
        /// <summary>Raw output entries resolved at factory time.</summary>
        public List<IngredientInfo> Output { get; set; }
    }
}
