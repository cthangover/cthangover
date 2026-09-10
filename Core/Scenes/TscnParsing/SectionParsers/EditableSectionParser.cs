using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[editable]</c> — editor-only placeholders for inherited scene instances. All content discarded.</summary>
    public class EditableSectionParser : ISectionParser
    {
        public string SectionType => "editable";
        public void Open(Dictionary<string, string> h) { }
        public void Property(string key, string rawValue) { }
        public void Close(ParsedScene s) { }
    }
}
