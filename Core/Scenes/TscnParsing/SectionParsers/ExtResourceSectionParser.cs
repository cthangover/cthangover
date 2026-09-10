using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[ext_resource]</c>. Each header is self-contained — extracts type, path, id; uid is stripped by lexer.</summary>
    public class ExtResourceSectionParser : ISectionParser
    {
        public string SectionType => "ext_resource";
        private ExtResourceRef _current;
        public void Open(Dictionary<string, string> h) { _current = new ExtResourceRef { Type = h.GetValueOrDefault("type"), Path = h.GetValueOrDefault("path"), Id = h.GetValueOrDefault("id") }; }
        public void Property(string key, string rawValue) { }
        public void Close(ParsedScene s) { if (_current != null) s.ExtResources.Add(_current); _current = null; }
    }
}
