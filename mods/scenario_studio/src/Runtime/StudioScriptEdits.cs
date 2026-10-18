namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using System.Text;
    using Cthangover.Mods.Dialog.Scenarios;
    using Godot;

    /// <summary>
    /// The DSL the dock writes. New lines are written in the current language
    /// as literals (what an author would type; the locale atlas convention for
    /// scenario text). Edits rewrite one line through
    /// <see cref="ScenarioParser.SplitLine"/> and keep its other arguments, so
    /// changing an answer's text does not lose its target and vice versa;
    /// a literal replaces a <c>key=</c>, because the new text is no longer the
    /// translated one.
    /// </summary>
    public static class StudioScriptEdits
    {
        public static string NewLine() => "text " + Quote(Tr("studio/new_line_text"));

        public static string NewQuestion() => "select " + Quote(Tr("studio/new_choice_text"));

        public static string NewAnswer() => "option " + Quote(Tr("studio/new_option_text"));

        public static string Jump(string scene) => "switch_scene " + scene;

        /// <summary><paramref name="line"/> with its player-visible text replaced by <paramref name="text"/>.</summary>
        public static string WithSay(string line, string text)
        {
            var positional = new List<string>();
            var named = new Dictionary<string, string>();
            var cmd = ScenarioParser.SplitLine(line.Trim(), positional, named, out var arrow);
            if (cmd == null)
                return line;

            named.Remove("key");
            if (positional.Count > 0)
                positional[0] = text;
            else
                positional.Insert(0, text);
            return Indent(line) + Compose(cmd, positional, named, arrow);
        }

        /// <summary><paramref name="line"/> (an answer or goto) pointing at label <paramref name="label"/>.</summary>
        public static string WithTarget(string line, string label)
        {
            var positional = new List<string>();
            var named = new Dictionary<string, string>();
            var cmd = ScenarioParser.SplitLine(line.Trim(), positional, named, out _);
            if (cmd == null)
                return line;

            named.Remove("goto");
            named.Remove("target");
            if (positional.Count > 1 && positional[1].StartsWith(':'))
                positional.RemoveAt(1);
            return Indent(line) + Compose(cmd, positional, named, label);
        }

        /// <summary>
        /// Makes sure the scenario has a label block that leads to scene
        /// <paramref name="scene"/> (<c>:to_scene</c> → <c>switch_scene</c> → <c>end</c>),
        /// appending it at the end when missing; returns the label name.
        /// </summary>
        public static string EnsureSceneExit(StudioWorkingCopy copy, string scene)
        {
            var label = "to_" + scene;
            if (copy.FindLabel(label) < 0)
                copy.Append("", ":" + label, Jump(scene), "end");
            return label;
        }

        /// <summary>Makes sure the scenario has a label that just ends it; returns the label name.</summary>
        public static string EnsureEnd(StudioWorkingCopy copy)
        {
            const string label = "finish";
            if (copy.FindLabel(label) < 0)
                copy.Append("", ":" + label, "end");
            return label;
        }

        public static string Quote(string text) => "\"" + (text ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static string Compose(string cmd, List<string> positional, Dictionary<string, string> named, string arrow)
        {
            var result = new StringBuilder(cmd);
            var sayCommand = cmd == "text" || cmd == "ptext" || cmd == "select" || cmd == "option";
            for (var i = 0; i < positional.Count; i++)
            {
                var value = positional[i];
                result.Append(' ').Append((i == 0 && sayCommand) || NeedsQuotes(value) ? Quote(value) : value);
            }
            foreach (var pair in named)
                result.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            if (!string.IsNullOrEmpty(arrow))
                result.Append(" -> :").Append(arrow);
            return result.ToString();
        }

        private static bool NeedsQuotes(string value) => value.Length == 0 || value.Contains(' ') || value.Contains('"') || value.Contains('=');

        private static string Indent(string line) => line.Substring(0, line.Length - line.TrimStart().Length);

        private static string Tr(string key) => TranslationServer.Translate(key).ToString();
    }
}
