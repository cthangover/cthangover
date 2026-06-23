using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Cards.StatusEffect
{

    [System.Serializable]
    public class StatusEffectInfo : IIdentifiable
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StatusEffectType Type { get; set; }
        public int Duration { get; set; }
        public string Actions { get; set; }
        public string Icon { get; set; }
    }
}
