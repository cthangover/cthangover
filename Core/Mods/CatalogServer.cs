using System;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>A player-managed catalog endpoint. The bearer itself lives in a separate private file.</summary>
    public sealed class CatalogServer
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
        [JsonPropertyName("token_file")]
        public string TokenFile { get; set; }
    }
}
