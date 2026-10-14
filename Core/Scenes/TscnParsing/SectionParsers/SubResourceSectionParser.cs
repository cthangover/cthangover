using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[sub_resource]</c>. Accumulates properties into a <see cref="SubResourceDef"/>; no Godot objects created here.</summary>
    public class SubResourceSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>sub_resource</c>.</summary>
        public string SectionType => "sub_resource";
        private SubResourceDef _current;
        /// <summary>Starts a sub_resource from the <c>type</c>/<c>id</c> header attributes.</summary>
        public void Open(Dictionary<string, string> h) { _current = new SubResourceDef { Type = h.GetValueOrDefault("type"), Id = h.GetValueOrDefault("id") }; }
        /// <summary>Parses each property line into a parsed value and records its source order.</summary>
        public void Property(string key, string rawValue) { if (_current != null) { _current.Properties[key] = TscnPropertyParser.Parse(rawValue); _current.PropertyOrder.Add(key); } }
        /// <summary>Commits the finished definition to <see cref="ParsedScene.SubResources"/>.</summary>
        public void Close(ParsedScene s) { if (_current != null) s.SubResources.Add(_current); _current = null; }
    }
}
