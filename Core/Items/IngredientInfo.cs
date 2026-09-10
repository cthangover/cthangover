using System;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// JSON deserialization DTO for a recipe ingredient entry. Carries
    /// only the item <c>ID</c> string and <c>Count</c> — not the resolved
    /// <c>IItem</c> — because recipe JSON files are loaded before item
    /// definitions may be fully populated across mods. The actual item
    /// reference is wired up later by <c>RecipeFactory.CreateIngredient</c>,
    /// which looks up the ID through <c>ItemFactory</c> and validates
    /// that the referenced item exists.
    /// </summary>
    [Serializable]
    public class IngredientInfo : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public int Count { get; set; }
    }
}
