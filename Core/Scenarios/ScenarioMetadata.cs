using System;
using System.Collections.Generic;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Extracts YAML-like front-matter metadata from scenario script text. Lines
    /// before a <c>---</c> separator or the first non-key:value line are parsed
    /// as <c>key: value</c> pairs. The command parsing half of the former
    /// ScenarioParser moved to the dialog_base engine mod; this metadata-only
    /// parser stays in the core because mod asset resolvers need it without a
    /// dialog dependency.
    /// </summary>
    public static class ScenarioMetadata
    {
        /// <summary>
        /// Returns both the metadata dictionary and the remaining script content
        /// after the metadata block.
        /// </summary>
        public static (Dictionary<string, string> metadata, string remainingText) ParseMetadata(string text)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = text.Split('\n');
            var contentStart = 0;
            var inMetadata = true;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (!inMetadata)
                {
                    continue;
                }

                if (line == "---")
                {
                    contentStart = i + 1;
                    break;
                }

                if (string.IsNullOrEmpty(line))
                {
                    contentStart = i;
                    break;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line.Substring(0, colonIdx).Trim();
                    var val = line.Substring(colonIdx + 1).Trim();
                    metadata[key] = val;
                }
                else
                {
                    contentStart = i;
                    break;
                }
            }

            var remaining = string.Join("\n", lines, contentStart, lines.Length - contentStart);
            return (metadata, remaining);
        }
    }
}