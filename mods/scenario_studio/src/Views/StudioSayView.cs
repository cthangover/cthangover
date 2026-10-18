namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// Novice card of a line the player reads (<c>.ct-say</c>): what kind of line
    /// it is (a question or a line, "new" while unapplied) and its text, with
    /// "Edit" on the right. The line on screen is the gold card; lines said
    /// right before it are plain cards with the green bar.
    /// </summary>
    public partial class StudioSayView : PanelContainer
    {
        public StudioSayView(StudioCodeLine line, bool before, Action<StudioActionButton> edit)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeStyleboxOverride("panel", StudioStyles.Say(before));

            var row = UiKit.Row(StudioStyles.BarGap);
            AddChild(row);
            row.AddChild(StudioStyles.Bar(before ? UiTokens.Success : UiTokens.Accent));

            var body = UiKit.Column(UiTokens.Space1);
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(body);

            var kind = TranslationServer.Translate(line.Kind == StudioLineKind.Choice ? "studio/say_question" : "studio/say_line").ToString();
            if (line.IsNew)
                kind = string.Format(TranslationServer.Translate("studio/say_new"), kind);
            var caption = StudioStyles.Text(kind, UiTokens.FontTiny, before ? UiTokens.Faint : UiTokens.AccentDim);
            caption.Uppercase = true;
            body.AddChild(caption);

            var text = UiKit.Label(Quote(line), before ? UiTokens.FontBodyLarge : UiTokens.FontLead, before ? UiTokens.Ink : UiTokens.InkBright);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(text);

            var button = StudioActionButton.Create("studio/edit", edit);
            button.Line = line;
            button.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(button);
        }

        /// <summary>The player-visible text in quotes, or an empty placeholder for a line that has none.</summary>
        public static string Quote(StudioCodeLine line)
        {
            var text = line.Say?.Resolve();
            return string.Format(TranslationServer.Translate("studio/quote"), string.IsNullOrEmpty(text) ? "…" : text);
        }
    }
}
