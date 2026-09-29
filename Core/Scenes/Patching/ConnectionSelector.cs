using System;
using Cthangover.Core.Scenes.TscnParsing;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// One entry of the <c>_patch_remove_connections</c> list. A selector is a set of
    /// <c>key=value</c> pairs joined with <c>;</c> — <c>from</c>, <c>to</c>, <c>signal</c>,
    /// <c>method</c> and <c>flags</c> are recognised. Omitted fields are wildcards, which
    /// lets a patch remove a connection by target (<c>to=</c>), by signal alone, or by any
    /// combination. At least <c>signal</c> and one of <c>from</c>/<c>to</c> are required:
    /// with neither, the selector could silently remove every connection of the scene.
    /// </summary>
    public class ConnectionSelector
    {
        /// <summary>Source node path (empty = wildcard).</summary>
        public string From { get; set; }

        /// <summary>Target node path (empty = wildcard).</summary>
        public string To { get; set; }

        /// <summary>Signal name (required by <see cref="TryParse"/>).</summary>
        public string Signal { get; set; }

        /// <summary>Method name (empty = wildcard).</summary>
        public string Method { get; set; }

        /// <summary>Connection flags (null = wildcard).</summary>
        public int? Flags { get; set; }

        /// <summary>
        /// Parses one selector string. Returns false with a human-readable
        /// <paramref name="error"/> for unknown keys, malformed flags, or a missing
        /// signal/source pair so the applier can log and skip instead of removing too much.
        /// </summary>
        public static bool TryParse(string text, out ConnectionSelector selector, out string error)
        {
            selector = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "selector is empty";
                return false;
            }

            var result = new ConnectionSelector();

            foreach (var rawPart in text.Split(';'))
            {
                var part = rawPart.Trim();
                if (part.Length == 0)
                    continue;

                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    error = $"selector part '{part}' is not key=value";
                    return false;
                }

                var key = part.Substring(0, eq).Trim().ToLowerInvariant();
                var value = part.Substring(eq + 1).Trim();
                if (value.Length == 0)
                {
                    error = $"selector key '{key}' has an empty value";
                    return false;
                }

                switch (key)
                {
                    case "from":
                        result.From = value;
                        break;
                    case "to":
                        result.To = value;
                        break;
                    case "signal":
                        result.Signal = value;
                        break;
                    case "method":
                        result.Method = value;
                        break;
                    case "flags":
                        if (!int.TryParse(value, out var flags))
                        {
                            error = $"selector flags '{value}' is not an integer";
                            return false;
                        }
                        result.Flags = flags;
                        break;
                    default:
                        error = $"unknown selector key '{key}'";
                        return false;
                }
            }

            if (string.IsNullOrEmpty(result.Signal))
            {
                error = "selector requires 'signal'";
                return false;
            }

            if (string.IsNullOrEmpty(result.From) && string.IsNullOrEmpty(result.To))
            {
                error = "selector requires 'from' or 'to'";
                return false;
            }

            selector = result;
            return true;
        }

        /// <summary>True when every specified field equals the connection's field.</summary>
        public bool Matches(ParsedConnection connection)
        {
            if (connection == null)
                return false;

            if (From != null && !string.Equals(From, connection.From, StringComparison.Ordinal))
                return false;
            if (To != null && !string.Equals(To, connection.To, StringComparison.Ordinal))
                return false;
            if (Signal != null && !string.Equals(Signal, connection.Signal, StringComparison.Ordinal))
                return false;
            if (Method != null && !string.Equals(Method, connection.Method, StringComparison.Ordinal))
                return false;
            if (Flags.HasValue && Flags.Value != connection.Flags)
                return false;

            return true;
        }
    }
}
