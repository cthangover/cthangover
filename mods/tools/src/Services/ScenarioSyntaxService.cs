using System.Collections.Generic;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Tools.Services
{
    /// <summary>
    /// Static service providing scenario DSL syntax highlighting, tokenisation,
    /// and command-line parsing. <see cref="CreateHighlighter"/> produces a
    /// <see cref="CodeHighlighter"/> for the scenario editor's
    /// <see cref="TextEdit"/>. <see cref="ExtractCommandParams"/> tokenises a
    /// scenario command line into a command name, positional arguments, and
    /// named (<c>key=value</c>) parameters — used by
    /// <see cref="ScenarioFileService.ExtractReferences"/> for reference analysis.
    /// </summary>
    public static class ScenarioSyntaxService
    {
        /// <summary>
        /// Builds a <see cref="CodeHighlighter"/> for scenario DSL syntax.
        /// Comments (lines starting with <c>#</c>) are green, strings are orange,
        /// command names are blue, and argument keywords (e.g. <c>key</c>,
        /// <c>wait</c>, <c>quest_id</c>) are amber.
        /// </summary>
        public static CodeHighlighter CreateHighlighter()
        {
            var hl = new CodeHighlighter();
            hl.AddColorRegion("#", null, new Color(0.45f, 0.75f, 0.45f), true);
            hl.AddColorRegion("\"", "\"", new Color(1f, 0.6f, 0.4f), false);
            hl.NumberColor = new Color(0.7f, 0.7f, 1f);
            hl.SymbolColor = new Color(0.8f, 0.8f, 0.8f);

            var cmdColor = new Color(0.4f, 0.7f, 1f);
            hl.KeywordColors = new Godot.Collections.Dictionary();

            foreach (var cmd in ScenarioCommandStrategyFactory.GetAllCommandNames())
                hl.KeywordColors[cmd] = cmdColor;
            
            foreach (var cmd in new[] {
                "option", "select"
            }) hl.KeywordColors[cmd] = cmdColor;
            
            var argColor = new Color(0.8f, 0.6f, 0.3f);
            foreach (var kw in new[] {
                "key", "first", "second", "hide_color", "wait", "speed", "hidden",
                "sprites", "loop", "target", "quest_id", "show", "hide", "duration"
            }) hl.KeywordColors[kw] = argColor;

            return hl;
        }

        /// <summary>
        /// Splits a line into tokens, handling quoted strings (with backslash
        /// escaping) and whitespace. Returns all non-whitespace tokens in order.
        /// </summary>
        public static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            var i = 0;
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i])) { i++; continue; }
                if (line[i] == '"')
                {
                    i++;
                    var start = i;
                    while (i < line.Length && line[i] != '"')
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) i++;
                        i++;
                    }
                    result.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++;
                    continue;
                }
                var tok = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                result.Add(line.Substring(tok, i - tok));
            }
            return result;
        }

        /// <summary>Strips surrounding double quotes from a value if present.</summary>
        public static string StripQuotes(string val)
        {
            if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                return val.Substring(1, val.Length - 2);
            return val;
        }

        /// <summary>
        /// Parses a scenario command line into its components. Tokenises via
        /// <see cref="Tokenize"/>, then extracts the first token as the command
        /// name (lowercased), remaining bare tokens as positional arguments, and
        /// <c>key=value</c> pairs as named arguments. The <c>-></c> arrow token
        /// is consumed but discarded.
        /// </summary>
        /// <param name="line">A single scenario command line (trimmed).</param>
        /// <param name="cmd">The command name, lowercased.</param>
        /// <param name="positional">Positional arguments in order.</param>
        /// <param name="named">Named arguments keyed by lowercased name.</param>
        public static void ExtractCommandParams(string line, out string cmd, out List<string> positional, out Dictionary<string, string> named)
        {
            positional = new List<string>();
            named = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            cmd = "";

            var tokens = Tokenize(line);
            if (tokens.Count == 0)
                return;

            cmd = tokens[0].ToLowerInvariant();

            for (int t = 1; t < tokens.Count; t++)
            {
                var tk = tokens[t];
                if (tk == "->") { t++; continue; }
                var eq = tk.IndexOf('=');
                if (eq > 0)
                    named[tk.Substring(0, eq).ToLowerInvariant()] = StripQuotes(tk.Substring(eq + 1));
                else
                    positional.Add(tk);
            }
        }
    }
}
