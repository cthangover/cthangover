namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "State" tab: game state grouped by owner (time, quests, inventory, dialog
    /// variables, completed scenarios). Values sit in editable fields because
    /// changing one is the quickest way to test another branch of a scenario;
    /// in the prototype the edits stay in the fields.
    /// </summary>
    public static class StudioStatePage
    {
        private const int KeyWidth = 170;

        public static void Build(VBoxContainer content, StudioSession session)
        {
            var hint = UiKit.Hint(TranslationServer.Translate("studio/state_hint"));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(hint);

            string group = null;
            VBoxContainer rows = null;
            foreach (var entry in session.State)
            {
                if (entry.Group.Key != group)
                {
                    group = entry.Group.Key;
                    content.AddChild(UiKit.SectionTitle(entry.Group.Resolve(), UiTokens.FontSmall));
                    var well = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                    well.AddThemeStyleboxOverride("panel", StudioStyles.Well());
                    content.AddChild(well);
                    rows = UiKit.Column(UiTokens.Space2);
                    well.AddChild(rows);
                }

                var row = UiKit.Row(UiTokens.Space3);
                var key = StudioStyles.Text(entry.Key.Resolve(), UiTokens.FontBodySmall, UiTokens.ChipKey, mono: true);
                key.CustomMinimumSize = new Vector2(KeyWidth, 0);
                key.ClipText = true;
                key.VerticalAlignment = VerticalAlignment.Center;
                row.AddChild(key);

                var value = new LineEdit { Text = entry.Value.Resolve(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                value.AddThemeFontOverride("font", StudioStyles.Mono);
                value.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                row.AddChild(value);
                rows.AddChild(row);
            }
        }
    }
}
