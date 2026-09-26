using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Validator
{
    /// <summary>
    /// A single machine-readable finding. The code catalogue
    /// (<c>SCHEMA_MISSING_FIELD</c>, <c>REF_NOT_FOUND</c>, <c>TYPE_CONFLICT</c>, …)
    /// is shared with the validator plan, so the report stays stable for LLM
    /// consumers while the set of checks grows.
    /// </summary>
    public class ValidationIssue
    {
        /// <summary><c>error</c>, <c>warning</c> or <c>info</c>.</summary>
        [JsonPropertyName("severity")]
        public string Severity { get; set; }

        /// <summary>Stable machine code of the check that produced the issue.</summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }

        /// <summary>Human-readable explanation.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>Mod the issue belongs to.</summary>
        [JsonPropertyName("mod")]
        public string Mod { get; set; }

        /// <summary>Mod-relative path of the offending file.</summary>
        [JsonPropertyName("file")]
        public string File { get; set; }

        /// <summary>One-based line number, or 0 when unknown.</summary>
        [JsonPropertyName("line")]
        public int Line { get; set; }

        /// <summary>Optional remediation hint.</summary>
        [JsonPropertyName("suggestion")]
        public string Suggestion { get; set; }

        /// <summary>Related locations in <c>mod:file:line</c> form, when the issue spans several usages.</summary>
        [JsonPropertyName("related")]
        public List<string> Related { get; set; }
    }
}
