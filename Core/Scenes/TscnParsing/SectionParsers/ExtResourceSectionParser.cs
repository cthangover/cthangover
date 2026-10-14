using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[ext_resource]</c>. Each header is self-contained — extracts type, path, id; uid is stripped by lexer.</summary>
    public class ExtResourceSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>ext_resource</c>.</summary>
        public string SectionType => "ext_resource";
        private ExtResourceRef _current;
        /// <summary>Starts an ext_resource from the <c>type</c>/<c>path</c>/<c>id</c> header attributes.</summary>
        public void Open(Dictionary<string, string> h) { _current = new ExtResourceRef { Type = h.GetValueOrDefault("type"), Path = h.GetValueOrDefault("path"), Id = h.GetValueOrDefault("id") }; }
        /// <summary>Ext_resource headers are self-contained — no-op.</summary>
        public void Property(string key, string rawValue) { }
        /// <summary>Commits the finished entry to <see cref="ParsedScene.ExtResources"/>.</summary>
        public void Close(ParsedScene s) { if (_current != null) s.ExtResources.Add(_current); _current = null; }
    }
}
