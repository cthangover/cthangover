namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// One line of the novice mode (<c>.ct-block</c>): a card with the kind of
    /// the line as a badge and its plain wording, without DSL syntax. Options
    /// are indented under their choice so a branching reads as a list of
    /// answers. Unapplied and broken lines keep the author-mode colours on the
    /// left bar, with the error spelled out under the text.
    /// </summary>
    public partial class StudioBlockView : MarginContainer
    {
        private const int OptionIndent = 22;

        public StudioBlockView(StudioCodeLine line)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeConstantOverride("margin_left", line.Kind == StudioLineKind.Option ? OptionIndent : 0);

            var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            card.AddThemeStyleboxOverride("panel", StudioStyles.Block(line));
            AddChild(card);

            var row = UiKit.Row(UiTokens.Space4);
            card.AddChild(row);

            var color = StudioStyles.KindColor(line.Kind);
            row.AddChild(UiKit.Badge(TranslationServer.Translate(KindKey(line.Kind)), color));

            var column = UiKit.Column(2);
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(column);

            var text = UiKit.Label(line.NoviceText.Resolve(), UiTokens.FontBody, line.IsCurrent ? UiTokens.InkBright : line.IsDone ? UiTokens.Muted : UiTokens.Body);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(text);

            if (line.Error != null)
                column.AddChild(UiKit.Label(line.Error.Resolve(), UiTokens.FontSmall, UiTokens.DangerText));
        }

        /// <summary>Locale key of the kind caption.</summary>
        public static string KindKey(StudioLineKind kind)
        {
            switch (kind)
            {
                case StudioLineKind.Light:
                    return "studio/kind_light";
                case StudioLineKind.Background:
                    return "studio/kind_background";
                case StudioLineKind.Choice:
                    return "studio/kind_choice";
                case StudioLineKind.Option:
                    return "studio/kind_option";
                case StudioLineKind.Label:
                    return "studio/kind_label";
                case StudioLineKind.Jump:
                    return "studio/kind_jump";
                case StudioLineKind.Action:
                    return "studio/kind_action";
                case StudioLineKind.End:
                    return "studio/kind_end";
                default:
                    return "studio/kind_line";
            }
        }
    }
}
