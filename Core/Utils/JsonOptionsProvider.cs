using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Central cache of shared <see cref="JsonSerializerOptions"/>. Mod data,
    /// saves and configs must all parse and serialize the same way, so the core
    /// hands out lazily created single instances instead of letting every
    /// subsystem configure its own (which also avoids rebuilding options per call).
    /// </summary>
    public static class JsonOptionsProvider
    {
        private static JsonSerializerOptions _readOptions;
        private static JsonSerializerOptions _writeIndentedOptions;
        private static JsonSerializerOptions _writeCompactOptions;
        private static JsonSerializerOptions _writeIndentedIgnoreNullOptions;

        /// <summary>
        /// Reader options shared by every subsystem: case-insensitive property names
        /// and skipped comments, so hand-edited mod files tolerate casing slips and
        /// stray commentary.
        /// </summary>
        public static JsonSerializerOptions ReadOptions
        {
            get
            {
                if (_readOptions == null)
                {
                    _readOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                }
                return _readOptions;
            }
        }

        /// <summary>
        /// Indented writer options — human-readable output for author-facing files
        /// the game rewrites (configs, tool-exported JSON).
        /// </summary>
        public static JsonSerializerOptions WriteIndentedOptions
        {
            get
            {
                if (_writeIndentedOptions == null)
                {
                    _writeIndentedOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                }
                return _writeIndentedOptions;
            }
        }

        /// <summary>
        /// Compact writer options — single-line output used for machine-facing
        /// payloads such as save slot preview metadata.
        /// </summary>
        public static JsonSerializerOptions WriteCompactOptions
        {
            get
            {
                if (_writeCompactOptions == null)
                {
                    _writeCompactOptions = new JsonSerializerOptions
                    {
                        WriteIndented = false
                    };
                }
                return _writeCompactOptions;
            }
        }

        /// <summary>
        /// Indented writer options that also omit <c>null</c> properties, keeping
        /// emitted JSON free of empty fields (used for optional-heavy DTOs).
        /// </summary>
        public static JsonSerializerOptions WriteIndentedIgnoreNullOptions
        {
            get
            {
                if (_writeIndentedIgnoreNullOptions == null)
                {
                    _writeIndentedIgnoreNullOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                }
                return _writeIndentedIgnoreNullOptions;
            }
        }

        private static JsonDocumentOptions _documentOptions;
        private static bool _documentOptionsInitialized;

        /// <summary>
        /// Options for <see cref="JsonDocument"/> parsing with comment skipping,
        /// matching <see cref="ReadOptions"/> for parse-only consumers.
        /// </summary>
        public static JsonDocumentOptions DocumentOptions
        {
            get
            {
                if (!_documentOptionsInitialized)
                {
                    _documentOptions = new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip
                    };
                    _documentOptionsInitialized = true;
                }
                return _documentOptions;
            }
        }
    }
}
