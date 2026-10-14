using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[editable]</c> — editor-only placeholders for inherited scene instances. All content discarded.</summary>
    public class EditableSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>editable</c> — accepted and discarded (editor-only inherited-scene placeholders).</summary>
        public string SectionType => "editable";
        /// <summary>No-op — the section carries no data the runtime builder needs.</summary>
        public void Open(Dictionary<string, string> h) { }
        /// <summary>No-op.</summary>
        public void Property(string key, string rawValue) { }
        /// <summary>No-op.</summary>
        public void Close(ParsedScene s) { }
    }
}
