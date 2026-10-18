namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Log" tab: what happened, newest last — scene entries, skipped scenarios
    /// with the reason, scenario starts and ends, stops and studio edits.
    /// Skips are tinted as warnings because they are the usual answer to
    /// "nothing happened".
    /// </summary>
    public static class StudioLogPage
    {
        private const int TimeWidth = 44;

        public static void Build(VBoxContainer content, StudioSession session)
        {
            var list = UiKit.Column(0);
            content.AddChild(list);

            foreach (var entry in session.Log)
            {
                var plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                plate.AddThemeStyleboxOverride("panel", StudioStyles.LogRow(entry.IsWarning));

                var row = UiKit.Row(UiTokens.Space3);
                plate.AddChild(row);

                var time = StudioStyles.Text(entry.Time, UiTokens.FontSmall, UiTokens.Faint, mono: true);
                time.CustomMinimumSize = new Vector2(TimeWidth, 0);
                row.AddChild(time);
                row.AddChild(UiKit.Badge(TranslationServer.Translate(CategoryKey(entry.Category)), StudioStyles.LogColor(entry.Category)));

                var text = UiKit.Label(entry.Text.Resolve(), UiTokens.FontBodySmall, entry.IsWarning ? UiTokens.Warning : UiTokens.Body);
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(text);
                list.AddChild(plate);
            }
        }

        private static string CategoryKey(StudioLogCategory category)
        {
            switch (category)
            {
                case StudioLogCategory.Scene:
                    return "studio/log_scene";
                case StudioLogCategory.Skip:
                    return "studio/log_skip";
                case StudioLogCategory.Stop:
                    return "studio/log_stop";
                case StudioLogCategory.End:
                    return "studio/log_end";
                case StudioLogCategory.Edit:
                    return "studio/log_edit";
                default:
                    return "studio/log_scenario";
            }
        }
    }
}
