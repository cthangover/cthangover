namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One <c>integrations</c> entry of a host manifest: glue that is wanted only when the host and
    /// every partner in <see cref="With"/> are in the game (a quest tag after a won battle needs quests).
    /// The folder (default <c>integrations/&lt;id&gt;</c>) holds a regular <c>manifest.json</c> with the
    /// glue's sources; it is mounted as the virtual mod <c>&lt;host&gt;__&lt;id&gt;</c> by
    /// <see cref="ModIntegrations"/>. Design: <c>docs/analysis/dependency-cleanup.md</c>.
    /// </summary>
    public sealed class ModIntegration
    {
        /// <summary>Integration id, unique within the host; the virtual mod is <c>&lt;host&gt;__&lt;id&gt;</c>.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Partner mod ids: the integration joins a mod set only when all of them are in it.</summary>
        [JsonPropertyName("with")]
        public List<string> With { get; set; }

        /// <summary>Mod-relative folder; <c>integrations/&lt;id&gt;</c> when omitted.</summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>The folder inside the host.</summary>
        [JsonIgnore]
        public string Folder => string.IsNullOrWhiteSpace(Path) ? $"{ModIntegrations.FolderName}/{Id}" : Path.Trim().TrimEnd('/');
    }
}
