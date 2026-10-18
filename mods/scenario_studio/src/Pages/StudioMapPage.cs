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
    /// leads there. Exits can be taken directly (a debugging teleport); new
    /// scenes are created in the studio layer, the current scenario can be
    /// linked to any scene, and the layer can be exported as a regular mod.
    /// </summary>
    public static class StudioMapPage
    {
        public static void Build(VBoxContainer content, StudioSession session, Action<StudioActionButton> go, Action<StudioActionButton> newScene, Action<StudioActionButton> link, Action<StudioActionButton> export)
        {
            var visited = false;
            foreach (var node in session.Nodes)
            {
                if (node.State != StudioNodeState.Visited)
                    continue;
                if (!visited)
                    content.AddChild(UiKit.SectionTitle(TranslationServer.Translate("studio/map_from"), UiTokens.FontSmall));
                visited = true;
                content.AddChild(BuildNode(node, go));
                content.AddChild(Arrow());
            }

            if (!visited)
                content.AddChild(UiKit.SectionTitle(TranslationServer.Translate("studio/map_here"), UiTokens.FontSmall));
            foreach (var node in session.Nodes)
            {
                if (node.State == StudioNodeState.Here)
                    content.AddChild(BuildNode(node, null));
            }

            content.AddChild(UiKit.SectionTitle(TranslationServer.Translate("studio/map_to"), UiTokens.FontSmall));
            var exits = 0;
            foreach (var node in session.Nodes)
            {
                if (node.State != StudioNodeState.Next && node.State != StudioNodeState.New)
                    continue;
                content.AddChild(BuildNode(node, go));
                exits++;
            }
            if (exits == 0)
                content.AddChild(UiKit.Hint(TranslationServer.Translate("studio/map_no_exits"), UiTokens.FontBodySmall));

            var actions = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            actions.AddThemeConstantOverride("h_separation", UiTokens.Space2);
            actions.AddThemeConstantOverride("v_separation", UiTokens.Space2);
            actions.AddChild(StudioActionButton.Create("studio/map_new_scene", newScene));
            if (session.HasScenario)
                actions.AddChild(StudioActionButton.Create("studio/map_link", link));
            actions.AddChild(StudioActionButton.Create("studio/map_export", export));
            content.AddChild(actions);
        }

        private static Control BuildNode(StudioSceneNode node, Action<StudioActionButton> go)
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

            if (node.State != StudioNodeState.Here)
            {
                var button = StudioActionButton.Create("studio/map_go", go);
                button.Payload = node.Name;
                button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(button);
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
