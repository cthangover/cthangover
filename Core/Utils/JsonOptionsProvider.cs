using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Utils
{
    public static class JsonOptionsProvider
    {
        private static JsonSerializerOptions _readOptions;
        private static JsonSerializerOptions _writeIndentedOptions;
        private static JsonSerializerOptions _writeCompactOptions;
        private static JsonSerializerOptions _writeIndentedIgnoreNullOptions;

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
