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
    public class RecipeInfo : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkbenchType Type { get; set; }
        public int Time { get; set; }
        public List<IngredientInfo> Input { get; set; }
        public List<IngredientInfo> Output { get; set; }
    }
}
