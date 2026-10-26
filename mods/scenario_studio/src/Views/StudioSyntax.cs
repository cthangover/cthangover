namespace Cthangover.Mods.ScenarioStudio
{
    using System.Text;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Dialog.Scenarios;
    using Godot;

    /// <summary>
    /// Colours one scenario DSL line as BBCode with the studio palette of the
    /// style guide: command word gold, quoted text ink, <c>:labels</c> teal,
    /// <c>name=</c> keys chip-key, comments faint. A light single-pass scanner
    /// is enough for display: it never fails on malformed lines, it just
    /// colours less of them, and the real parser stays the only judge of
    /// validity.
    /// </summary>
    public static class StudioSyntax
    {
        /// <summary>Uses the same scenario syntax as the full editor and embedded command fields.</summary>
        public static CodeHighlighter CreateHighlighter()
            => SourceSyntax.CreateScenarioHighlighter(ScenarioCommandStrategyFactory.GetAllCommandNames());

        public static string ToBbCode(string line)
        {
            var text = line ?? string.Empty;
            var trimmed = text.TrimStart();
            var indent = text.Substring(0, text.Length - trimmed.Length);
            var result = new StringBuilder(Escape(indent));

            if (trimmed.StartsWith('#'))
                return result.Append(Paint(trimmed, UiTokens.Faint)).ToString();
            if (trimmed.StartsWith(':'))
                return result.Append(Paint(trimmed, UiTokens.TypeTarget)).ToString();

            var i = 0;
            var first = true;
            while (i < trimmed.Length)
            {
                var c = trimmed[i];
                if (c == ' ')
                {
                    result.Append(' ');
                    i++;
                    continue;
                }

                if (c == '#')
                {
                    result.Append(Paint(trimmed.Substring(i), UiTokens.Faint));
                    break;
                }

                var end = c == '"' ? CloseQuote(trimmed, i) : NextSpace(trimmed, i);
                var token = trimmed.Substring(i, end - i);
                result.Append(PaintToken(token, first));
                first = false;
                i = end;
            }

            return result.ToString();
        }

        private static string PaintToken(string token, bool first)
        {
            if (first)
                return Paint(token, UiTokens.AccentBright);
            if (token.StartsWith('"'))
                return Paint(token, UiTokens.Ink);
            if (token.StartsWith(':'))
                return Paint(token, UiTokens.TypeTarget);
            if (token == "->")
                return Paint(token, UiTokens.Muted);

            var eq = token.IndexOf('=');
            if (eq > 0)
                return Paint(token.Substring(0, eq + 1), UiTokens.ChipKey) + Paint(token.Substring(eq + 1), UiTokens.Body);
            return Paint(token, UiTokens.Body);
        }

        private static int CloseQuote(string text, int start)
        {
            var close = text.IndexOf('"', start + 1);
            return close < 0 ? text.Length : close + 1;
        }

        private static int NextSpace(string text, int start)
        {
            var space = text.IndexOf(' ', start);
            return space < 0 ? text.Length : space;
        }

        private static string Paint(string text, Color color) => $"[color=#{color.ToHtml(false)}]{Escape(text)}[/color]";

        private static string Escape(string text) => text.Replace("[", "[lb]");
    }
}
