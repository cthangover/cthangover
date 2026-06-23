using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    public class ModManifest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("sources")]
        public List<string> Sources { get; set; }

        [JsonPropertyName("depends")]
        public List<string> Depends { get; set; }

        [JsonPropertyName("subscriptions")]
        public List<SubscriptionEntry> Subscriptions { get; set; }
    }
}
