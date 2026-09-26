using System.Text.Json.Serialization;

namespace Cthangover.Validator
{
    /// <summary>
    /// Per-mod section of the report. <c>status</c> is <c>ok</c> when the mod
    /// contributed no errors, otherwise <c>errors</c> — the field lets a
    /// consumer triage mods without scanning the flat issue list.
    /// </summary>
    public class ValidationModStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("install_sha256")]
        public string InstallSha256 { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ok";
    }
}
