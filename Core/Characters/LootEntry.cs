using System.Text.Json.Serialization;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Loot drop definition for enemies: ItemId, min/max count range, and
    /// probability (presumably 0–100 percentage). Uses JsonPropertyName
    /// attributes for PascalCase JSON keys matching Godot's C# serialization
    /// conventions. The count range allows variable drops (e.g. "1–3 potions").
    /// </summary>
    public class LootEntry
    {
        [JsonPropertyName("ItemId")]
        public string ItemId { get; set; }

        [JsonPropertyName("MinCount")]
        public int MinCount { get; set; }

        [JsonPropertyName("MaxCount")]
        public int MaxCount { get; set; }

        [JsonPropertyName("Probability")]
        public int Probability { get; set; }
    }

}
