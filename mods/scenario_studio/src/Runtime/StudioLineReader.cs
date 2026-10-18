namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary>
    /// Reads scenario text into <see cref="StudioCodeLine"/>s for the dock: one
    /// entry per non-empty line, numbered like the parser numbers
    /// <c>SourceLine</c> (1-based, header included), classified by command
    /// with the player-visible text (<see cref="StudioCodeLine.Say"/>: the
    /// literal, or the line's own <c>key=</c> translated when shown) and the
    /// line's target. Lines are split by <see cref="ScenarioParser.SplitLine"/>
    /// so the dock never disagrees with the engine; on top of the parser it
    /// reports what the parser silently skips — unknown commands, answers and
    /// jumps to missing labels.
    /// </summary>
    public static class StudioLineReader
    {
        public static List<StudioCodeLine> Read(string text)
        {
            var result = new List<StudioCodeLine>();
            if (string.IsNullOrEmpty(text))
                return result;

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var headerEnd = HeaderEnd(lines);
            var labels = new HashSet<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0)
                    continue;

                var line = new StudioCodeLine { Number = i + 1, Text = lines[i].TrimEnd() };
                if (i <= headerEnd)
                    line.Kind = StudioLineKind.Header;
                else
                    Classify(line, trimmed);

                if (line.Kind == StudioLineKind.Label)
                    labels.Add(line.Target);
                result.Add(line);
            }

            foreach (var line in result)
                CheckTarget(line, labels);
            return result;
        }

        private static int HeaderEnd(string[] lines)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---")
                    return i;
            }
            return -1;
        }

        private static void Classify(StudioCodeLine line, string trimmed)
        {
            if (trimmed.StartsWith('#'))
            {
                line.Kind = StudioLineKind.Comment;
                return;
            }

            if (trimmed.StartsWith(':'))
            {
                line.Kind = StudioLineKind.Label;
                line.Target = trimmed.Substring(1).Trim();
                return;
            }

            var positional = new List<string>();
            var named = new Dictionary<string, string>();
            var cmd = ScenarioParser.SplitLine(trimmed, positional, named, out var arrow);
            switch (cmd)
            {
                case "text":
                case "ptext":
                    line.Kind = StudioLineKind.Line;
                    line.Say = SayOf(positional, named);
                    return;
                case "select":
                    line.Kind = StudioLineKind.Choice;
                    line.Say = SayOf(positional, named);
                    return;
                case "option":
                    line.Kind = StudioLineKind.Option;
                    line.Say = SayOf(positional, named);
                    line.Target = OptionTarget(positional, named, arrow);
                    return;
                case "goto":
                    line.Kind = StudioLineKind.Goto;
                    line.Target = !string.IsNullOrEmpty(arrow) ? arrow : named.GetValueOrDefault("target");
                    return;
                case "switch_scene":
                    line.Kind = StudioLineKind.Jump;
                    line.Target = positional.Count > 0 ? positional[0] : null;
                    return;
                case "action":
                    line.Kind = StudioLineKind.Action;
                    line.Target = positional.Count > 0 ? positional[0] : null;
                    return;
                case "end":
                    line.Kind = StudioLineKind.End;
                    return;
                case "light_set":
                case "light_use_time":
                    line.Kind = StudioLineKind.Light;
                    return;
                case "background":
                case "foreground":
                    line.Kind = StudioLineKind.Background;
                    return;
            }

            line.Kind = StudioLineKind.Command;
            if (cmd != null && ScenarioCommandStrategyFactory.Get(cmd) == null)
                line.Error = new StudioText("studio/error_unknown_command", cmd);
        }

        private static StudioText SayOf(List<string> positional, Dictionary<string, string> named)
        {
            if (named.TryGetValue("key", out var key) && !string.IsNullOrEmpty(key))
                return new StudioText(key);
            return positional.Count > 0 ? StudioText.Raw(positional[0]) : null;
        }

        private static string OptionTarget(List<string> positional, Dictionary<string, string> named, string arrow)
        {
            if (!string.IsNullOrEmpty(arrow))
                return arrow;
            if (named.TryGetValue("goto", out var target))
                return target;
            return positional.Count > 1 && positional[1].StartsWith(':') ? positional[1].TrimStart(':') : null;
        }

        private static void CheckTarget(StudioCodeLine line, HashSet<string> labels)
        {
            if (line.Kind != StudioLineKind.Option && line.Kind != StudioLineKind.Goto)
                return;
            if (string.IsNullOrEmpty(line.Target))
                line.Error ??= new StudioText("studio/error_no_target");
            else if (!labels.Contains(line.Target))
                line.Error ??= new StudioText("studio/error_label_missing", ":" + line.Target);
        }
    }
}
