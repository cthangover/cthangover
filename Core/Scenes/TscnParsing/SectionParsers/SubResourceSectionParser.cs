using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[sub_resource]</c>. Accumulates properties into a <see cref="SubResourceDef"/>; no Godot objects created here.</summary>
    public class SubResourceSectionParser : ISectionParser
    {
        public string SectionType => "sub_resource";
        private SubResourceDef _current;
        public void Open(Dictionary<string, string> h) { _current = new SubResourceDef { Type = h.GetValueOrDefault("type"), Id = h.GetValueOrDefault("id") }; }
        public void Property(string key, string rawValue) { if (_current != null) { _current.Properties[key] = TscnPropertyParser.Parse(rawValue); _current.PropertyOrder.Add(key); } }
        public void Close(ParsedScene s) { if (_current != null) s.SubResources.Add(_current); _current = null; }
    }
}
