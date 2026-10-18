namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "State" tab: game state grouped by owner — time, dialog variables of the
    /// running dialog, quests the current scenario refers to, inventory and the
    /// one-run completion history. Variables, item counts and quest status are
    /// editable (Enter writes them into the game), because changing one is the
    /// quickest way to test another branch of a scenario.
    /// </summary>
    public static class StudioStatePage
    {
        public static void Build(VBoxContainer content, StudioSession session, Action<StudioStateEntry, string> submitted)
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

                var row = new StudioStateRow(entry);
                row.Submitted += submitted;
                rows.AddChild(row);
            }
        }
    }
}
