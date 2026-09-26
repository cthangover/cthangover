using System.Text.Json.Serialization;

namespace Cthangover.Validator
{
    /// <summary>
    /// Aggregated issue counters placed at the top of the report so consumers can
    /// decide whether to read the full issue list without deserialising it.
    /// </summary>
    public class ValidationSummary
    {
        [JsonPropertyName("errors")]
        public int Errors { get; set; }

        [JsonPropertyName("warnings")]
        public int Warnings { get; set; }

        [JsonPropertyName("infos")]
        public int Infos { get; set; }
    }
}
