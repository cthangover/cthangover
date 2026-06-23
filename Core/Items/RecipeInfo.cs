using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Items
{
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
