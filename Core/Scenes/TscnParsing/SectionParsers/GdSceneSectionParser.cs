using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[gd_scene]</c>. Extracts format; uid and load_steps are stripped by the lexer.</summary>
    public class GdSceneSectionParser : ISectionParser
    {
        public string SectionType => "gd_scene";
        private string _format;
        public void Open(Dictionary<string, string> headerAttrs) { if (headerAttrs.TryGetValue("format", out var f)) _format = f; }
        public void Property(string key, string rawValue) { }
        public void Close(ParsedScene scene) { if (_format != null) scene.Format = _format; }
    }
}
