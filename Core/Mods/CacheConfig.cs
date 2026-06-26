using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for the <c>cache</c> section of <c>mod_config.json</c>.
    /// The <c>MaxSizes</c> dictionary maps factory keys (e.g. "backgrounds",
    /// "avatars") to explicit LRU capacity overrides — factories query
    /// this via <c>GetCacheSize(key, fallback)</c> at construction time so
    /// that memory budgets can be tuned per asset category without
    /// recompiling.
    /// </summary>
    public class CacheConfig
    {
        /// <summary>
        /// Godot path to the on-disk cache root (e.g. "user://mod_cache/").
        /// Shaders extracted from zip mods land here so Godot can load them.
        /// </summary>
        [JsonPropertyName("root")]
        public string Root { get; set; } = "user://mod_cache/";

        /// <summary>
        /// Per-factory LRU capacity overrides keyed by factory name
        /// (e.g. {"backgrounds": 32, "avatars": 128}). Negative values
        /// are treated as "unset" and fall through to the default.
        /// </summary>
        [JsonPropertyName("max_sizes")]
        public Dictionary<string, int> MaxSizes { get; set; } = new();

        /// <summary>
        /// Resolves the LRU cache size for a factory: returns the
        /// override from <c>MaxSizes</c> if present and non-negative,
        /// otherwise the caller-supplied fallback.
        /// </summary>
        public int GetCacheSize(string factoryKey, int fallback)
        {
            if (MaxSizes.TryGetValue(factoryKey, out var size) && size >= 0)
                return size;
            return fallback;
        }
    }
}
