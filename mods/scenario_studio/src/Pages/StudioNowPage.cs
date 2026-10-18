namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Now" tab: the running scenario around the debugger position plus the
    /// tools to extend it on the spot. The author mode shows the script with a
    /// gutter (<see cref="StudioCodeLineView"/>), the novice mode the same lines
    /// as cards (<see cref="StudioBlockView"/>) with the insert bar placed right
    /// under the current card, where the new line will play. Both end with the
    /// "this change affects" plate naming the mod that receives the edit and the
    /// mod it overrides, so nobody changes someone else's content unknowingly.
    /// <para/>
    /// One instance lives as long as the dock; <see cref="Build"/> refills the
    /// body and wires the controls to this instance's handlers.
    /// </summary>
    public sealed class StudioNowPage
    {
        /// <summary>A gutter click changed a breakpoint.</summary>
        public event Action<StudioCodeLine> BreakpointToggled;

        /// <summary>One of the insert buttons was pressed.</summary>
        public event Action<StudioLineKind> InsertRequested;

        public void Build(VBoxContainer content, StudioSession session, StudioMode mode)
        {
            if (mode == StudioMode.Author)
                BuildAuthor(content, session);
            else
                BuildNovice(content, session);

            content.AddChild(BuildScopeBanner(session));
        }

        private void BuildAuthor(VBoxContainer content, StudioSession session)
        {
            var well = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            well.AddThemeStyleboxOverride("panel", StudioStyles.CodeWell());
            content.AddChild(well);

            var lines = UiKit.Column(0);
            well.AddChild(lines);
            foreach (var line in session.Lines)
            {
                var view = new StudioCodeLineView(line);
                view.BreakpointToggled += OnBreakpointToggled;
                lines.AddChild(view);
            }

            var current = session.CurrentIndex;
            var caption = current < 0 ? string.Empty : string.Format(TranslationServer.Translate("studio/after_line"), session.Lines[current].Number);
            content.AddChild(BuildInsertBar(caption, large: false));
        }

        private void BuildNovice(VBoxContainer content, StudioSession session)
        {
            var list = UiKit.Column(UiTokens.Space2);
            content.AddChild(list);
            foreach (var line in session.Lines)
            {
                list.AddChild(new StudioBlockView(line));
                if (line.IsCurrent)
                    list.AddChild(BuildInsertBar(string.Empty, large: true));
            }
        }

        private Control BuildInsertBar(string caption, bool large)
        {
            var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeStyleboxOverride("panel", StudioStyles.InsertBar());

            var row = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("h_separation", UiTokens.Space2);
            row.AddThemeConstantOverride("v_separation", UiTokens.Space2);
            bar.AddChild(row);

            if (!string.IsNullOrEmpty(caption))
            {
                var label = StudioStyles.Text(caption, UiTokens.FontSmall, UiTokens.Faint);
                label.VerticalAlignment = VerticalAlignment.Center;
                label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(label);
            }

            row.AddChild(InsertButton("studio/add_line", large, OnAddLine));
            row.AddChild(InsertButton("studio/add_choice", false, OnAddChoice));
            row.AddChild(InsertButton("studio/add_jump", false, OnAddJump));
            return bar;
        }

        private static Button InsertButton(string key, bool primary, Action handler)
        {
            var button = StudioStyles.SmallButton(new Button(), TranslationServer.Translate(key), primary);
            button.Pressed += handler;
            return button;
        }

        private static Control BuildScopeBanner(StudioSession session)
        {
            var banner = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            banner.AddThemeStyleboxOverride("panel", StudioStyles.ScopeBanner());

            var column = UiKit.Column(UiTokens.Space2);
            banner.AddChild(column);

            var title = StudioStyles.Text(TranslationServer.Translate("studio/affects"), UiTokens.FontTiny, UiTokens.Warning);
            title.Uppercase = true;
            column.AddChild(title);

            var row = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("h_separation", UiTokens.Space3);
            column.AddChild(row);
            row.AddChild(StudioStyles.Scope(session.EditScope));
            if (session.EditScope.ModId != session.FileScope.ModId)
            {
                row.AddChild(StudioStyles.Text(TranslationServer.Translate("studio/overrides"), UiTokens.FontSmall, UiTokens.Faint));
                row.AddChild(StudioStyles.Scope(session.FileScope));
            }

            return banner;
        }

        private void OnBreakpointToggled(StudioCodeLine line) => BreakpointToggled?.Invoke(line);

        private void OnAddLine() => InsertRequested?.Invoke(StudioLineKind.Line);

        private void OnAddChoice() => InsertRequested?.Invoke(StudioLineKind.Choice);

        private void OnAddJump() => InsertRequested?.Invoke(StudioLineKind.Jump);
    }
}
