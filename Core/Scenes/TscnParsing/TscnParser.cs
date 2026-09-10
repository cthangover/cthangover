using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Orchestrator for .tscn parsing. Delegates section handling to <see cref="SectionParserRegistry"/> —
    /// no section-specific logic lives here. Produces a <see cref="ParsedScene"/> without creating Godot objects.
    /// </summary>
    public class TscnParser
    {
        /// <summary>Parses .tscn source text into a ParsedScene. The scene contains flat lists of resources, nodes, and connections ready for <see cref="TscnBuild.SceneBuilder"/>.</summary>
        public ParsedScene Parse(string source)
        {
            var tokens = new TscnLexer().Tokenize(source);
            var scene = new ParsedScene();
            ISectionParser current = null;
            foreach (var token in tokens)
            {
                if (token.Type == TscnTokenType.SectionHeader)
                {
                    current?.Close(scene);
                    current = SectionParserRegistry.Get(token.SectionName);
                    current?.Open(token.SectionAttrs ?? new Dictionary<string, string>());
                }
                else if (token.Type == TscnTokenType.Property && current != null)
                {
                    current.Property(token.PropertyKey, token.PropertyValue);
                }
            }
            current?.Close(scene);
            return scene;
        }
    }
}
