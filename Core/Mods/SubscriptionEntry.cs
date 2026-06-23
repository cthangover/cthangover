using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    public class SubscriptionEntry
    {
        [JsonPropertyName("scene")]
        public string Scene { get; set; }

        [JsonPropertyName("template")]
        public string Template { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 10;

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = "on_enter";

        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
