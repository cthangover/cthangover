namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Map" tab: the walk between scenes around the player, laid out as a
    /// vertical path because the dock is narrow — where the player came from,
    /// where they are, and the exits from here with the command or choice that
    /// leads there. Exits can be taken directly (a debugging teleport), and new
    /// scenes and links are created from here; a new scene carries the scope
    /// badge of the studio layer that will hold it.
    /// </summary>
    public static class StudioMapPage
    {
        public static void Build(VBoxContainer content, StudioSession session, Action<string> picked)
        {
            content.AddChild(UiKit.SectionTitle(TranslationServer.Translate("studio/map_from"), UiTokens.FontSmall));
            foreach (var node in session.Nodes)
            {
                if (node.State != StudioNodeState.Visited)
                    continue;
                content.AddChild(BuildNode(node, null));
                content.AddChild(Arrow());
            }

            foreach (var node in session.Nodes)
            {
                if (node.State == StudioNodeState.Here)
                    content.AddChild(BuildNode(node, null));
            }

            content.AddChild(UiKit.SectionTitle(TranslationServer.Translate("studio/map_to"), UiTokens.FontSmall));
            foreach (var node in session.Nodes)
            {
                if (node.State == StudioNodeState.Next || node.State == StudioNodeState.New)
                    content.AddChild(BuildNode(node, picked));
            }

            var actions = UiKit.Row(UiTokens.Space2);
            actions.AddChild(StudioPrototypeButton.Create("studio/map_new_scene", picked));
            actions.AddChild(StudioPrototypeButton.Create("studio/map_link", picked));
            content.AddChild(actions);
        }

        private static Control BuildNode(StudioSceneNode node, Action<string> picked)
        {
            var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            card.AddThemeStyleboxOverride("panel", StudioStyles.Node(node.State));

            var row = UiKit.Row(UiTokens.Space3);
            card.AddChild(row);

            var column = UiKit.Column(UiTokens.Space1);
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(column);

            var head = UiKit.Row(UiTokens.Space3);
            var nameColor = node.State == StudioNodeState.Here ? UiTokens.AccentBright : UiTokens.Ink;
            head.AddChild(StudioStyles.Text(node.Name, UiTokens.FontBodySmall, nameColor, mono: true));
            head.AddChild(StudioStyles.Scope(node.Scope));
            column.AddChild(head);

            var sub = TranslationServer.Translate(StateKey(node.State));
            if (node.Via != null)
                sub += " · " + node.Via.Resolve();
            column.AddChild(StudioStyles.Text(sub, UiTokens.FontSmall, UiTokens.Faint));

            if (picked != null)
            {
                var go = StudioPrototypeButton.Create("studio/map_go", picked);
                go.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(go);
            }

            return card;
        }

        private static Control Arrow()
        {
            var arrow = StudioStyles.Text("↓", UiTokens.FontBody, UiTokens.Faint);
            arrow.HorizontalAlignment = HorizontalAlignment.Center;
            return arrow;
        }

        private static string StateKey(StudioNodeState state)
        {
            switch (state)
            {
                case StudioNodeState.Here:
                    return "studio/node_here";
                case StudioNodeState.Visited:
                    return "studio/node_visited";
                case StudioNodeState.New:
                    return "studio/node_new";
                default:
                    return "studio/node_next";
            }
        }
    }
}
