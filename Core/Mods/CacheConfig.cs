using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    public class CacheConfig
    {
        [JsonPropertyName("root")]
        public string Root { get; set; } = "user://mod_cache/";

        [JsonPropertyName("max_sizes")]
        public Dictionary<string, int> MaxSizes { get; set; } = new();

        public int GetCacheSize(string factoryKey, int fallback)
        {
            if (MaxSizes.TryGetValue(factoryKey, out var size) && size >= 0)
                return size;
            return fallback;
        }
    }
}
