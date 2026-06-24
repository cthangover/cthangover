using System.Collections.Generic;
using Cthangover.Core.Scenarios;
using Godot;

namespace Cthangover.Tools.Services
{
    public static class ScenarioSyntaxService
    {
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

        public static string StripQuotes(string val)
        {
            if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                return val.Substring(1, val.Length - 2);
            return val;
        }

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
