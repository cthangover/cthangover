using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One <c>test_suites</c> entry of a host manifest: the suite id, its
    /// folder (default <c>test_suites/&lt;id&gt;</c>, holding a regular
    /// <c>manifest.json</c> plus sources and content) and the user-arg prefixes
    /// that mount it (e.g. <c>--itest=interactives</c>).
    /// </summary>
    public sealed class ModTestSuite
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Mod-relative folder of the suite; <c>test_suites/&lt;id&gt;</c> when omitted.</summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>User-arg prefixes (after <c>--</c>) that mount the suite; any match is enough.</summary>
        [JsonPropertyName("activate")]
        public List<string> Activate { get; set; }

        [JsonIgnore]
        public string Folder => string.IsNullOrWhiteSpace(Path) ? $"{ModTestSuites.FolderName}/{Id}" : Path.Trim().TrimEnd('/');
    }
}
