using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[connection]</c>. Only script-method connections (to=".") are kept; animation/visual connections are dropped.</summary>
    public class ConnectionSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>connection</c>.</summary>
        public string SectionType => "connection";
        private ParsedConnection _current;
        /// <summary>Starts a connection from the header attributes; <c>flags</c> is parsed as int when present.</summary>
        public void Open(Dictionary<string, string> h) { _current = new ParsedConnection { Signal = h.GetValueOrDefault("signal"), From = h.GetValueOrDefault("from"), To = h.GetValueOrDefault("to"), Method = h.GetValueOrDefault("method") }; if (h.TryGetValue("flags", out var f) && int.TryParse(f, out var flags)) _current.Flags = flags; }
        /// <summary>Connections carry no property lines — no-op.</summary>
        public void Property(string key, string rawValue) { }
        /// <summary>Commits only root-method connections (to "." or empty); animation/visual targets are dropped.</summary>
        public void Close(ParsedScene s) { if (_current != null && !string.IsNullOrEmpty(_current.Method) && (_current.To == "." || _current.To == "")) s.Connections.Add(_current); _current = null; }
    }
}
