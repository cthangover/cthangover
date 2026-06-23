using System.Text.Json.Serialization;

namespace Cthangover.Core.Characters
{

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
