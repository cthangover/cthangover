using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Validator
{
    /// <summary>
    /// Root DTO of the machine-readable validation report written to
    /// <c>user://validation/report.json</c>. Snake_case JSON names are fixed here
    /// rather than in serialiser options, so the format cannot drift when global
    /// options change.
    /// </summary>
    public class ValidationReport
    {
        /// <summary>UTC timestamp in round-trip format.</summary>
        [JsonPropertyName("generated_at")]
        public string GeneratedAt { get; set; }

        /// <summary>Game version the report was produced for.</summary>
        [JsonPropertyName("game_version")]
        public string GameVersion { get; set; }

        /// <summary>Validator mod version; part of the report cache key.</summary>
        [JsonPropertyName("validator_version")]
        public string ValidatorVersion { get; set; }

        /// <summary>Issue counters.</summary>
        [JsonPropertyName("summary")]
        public ValidationSummary Summary { get; set; } = new();

        /// <summary>Validated mods in load order with their status.</summary>
        [JsonPropertyName("mods")]
        public List<ValidationModStatus> Mods { get; set; } = new();

        /// <summary>All findings of the pass.</summary>
        [JsonPropertyName("issues")]
        public List<ValidationIssue> Issues { get; set; } = new();
    }
}
