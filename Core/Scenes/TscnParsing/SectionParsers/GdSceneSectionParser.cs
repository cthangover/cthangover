using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>Parses <c>[gd_scene]</c>. Extracts format; uid and load_steps are stripped by the lexer.</summary>
    public class GdSceneSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>gd_scene</c>.</summary>
        public string SectionType => "gd_scene";
        private string _format;
        /// <summary>Captures the <c>format=</c> attribute (information only).</summary>
        public void Open(Dictionary<string, string> headerAttrs) { if (headerAttrs.TryGetValue("format", out var f)) _format = f; }
        /// <summary>The scene header carries no property lines — no-op.</summary>
        public void Property(string key, string rawValue) { }
        /// <summary>Copies the captured format value into <see cref="ParsedScene.Format"/>.</summary>
        public void Close(ParsedScene scene) { if (_format != null) scene.Format = _format; }
    }
}
