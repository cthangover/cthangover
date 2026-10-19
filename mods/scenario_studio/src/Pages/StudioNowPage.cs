namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Now" tab in the author mode: the running scenario as script with a
    /// gutter (<see cref="StudioCodeLineView"/>) and the insert bar for a line
    /// after the current one. It ends with the "this change affects" plate
    /// naming the mod that receives the edit and the mod it overrides, so
    /// nobody changes someone else's content unknowingly; the novice mode
    /// (<see cref="StudioNovicePage"/>) shows the same plate.
    /// <para/>
    /// Dragging the ▶ marker of the current line highlights the line under the
    /// mouse when the running dialog can continue from it
    /// (<see cref="StudioCodeLine.CanRunNext"/>) and on release asks for it
    /// with <see cref="NextLineRequested"/>; a drop anywhere else does nothing.
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

        /// <summary>The ▶ marker was dropped on a line the dialog can continue from.</summary>
        public event Action<StudioCodeLine> NextLineRequested;

        private readonly List<StudioCodeLineView> _views = new();
        private StudioCodeLineView _dropTarget;
        private bool _dragging;

        /// <summary>True while the ▶ marker is dragged: a redraw would drop the drag.</summary>
        public bool IsDragging => _dragging;

        public void Build(VBoxContainer content, StudioSession session)
        {
            _views.Clear();
            _dropTarget = null;
            _dragging = false;
            if (!session.HasScenario)
            {
                content.AddChild(UiKit.EmptyState(TranslationServer.Translate("studio/no_scenario"), UiTokens.FontBody));
                return;
            }

            var well = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            well.AddThemeStyleboxOverride("panel", StudioStyles.CodeWell());
            content.AddChild(well);

            var lines = UiKit.Column(0);
            well.AddChild(lines);
            foreach (var line in session.Lines)
            {
                var view = new StudioCodeLineView(line);
                view.BreakpointToggled += OnBreakpointToggled;
                view.MarkerDragged += OnMarkerDragged;
                view.MarkerDropped += OnMarkerDropped;
                lines.AddChild(view);
                _views.Add(view);
            }

            var at = session.InsertionTextIndexAfterCurrent();
            var caption = at <= 0 ? string.Empty : string.Format(TranslationServer.Translate("studio/after_line"), at);
            content.AddChild(BuildInsertBar(caption));
            content.AddChild(BuildScopeBanner(session));
        }

        private Control BuildInsertBar(string caption)
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

            row.AddChild(InsertButton("studio/add_line", OnAddLine));
            row.AddChild(InsertButton("studio/add_choice", OnAddChoice));
            row.AddChild(InsertButton("studio/add_jump", OnAddJump));
            return bar;
        }

        private static Button InsertButton(string key, Action handler)
        {
            var button = StudioStyles.SmallButton(new Button(), TranslationServer.Translate(key));
            button.Pressed += handler;
            return button;
        }

        /// <summary>
        /// Inline editor of the whole scenario text (the footer's "Edit text"),
        /// filling the body; the dock reads the text back when editing ends.
        /// </summary>
        public static CodeEdit BuildEditor(VBoxContainer content, string text)
        {
            var editor = new CodeEdit
            {
                Text = text ?? string.Empty,
                GuttersDrawLineNumbers = true,
                HighlightCurrentLine = true,
                WrapMode = TextEdit.LineWrappingMode.None,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 560),
                SyntaxHighlighter = StudioSyntax.CreateHighlighter(),
            };
            editor.AddThemeFontOverride("font", StudioStyles.Mono);
            editor.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            content.AddChild(editor);
            return editor;
        }

        /// <summary>"This change affects" plate: the edit scope and, when it differs, the mod it overrides.</summary>
        public static Control BuildScopeBanner(StudioSession session)
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
            if (session.EditScope == null)
                return banner;
            row.AddChild(StudioStyles.Scope(session.EditScope));
            if (session.EditScope.ModId != session.FileScope.ModId)
            {
                row.AddChild(StudioStyles.Text(TranslationServer.Translate("studio/overrides"), UiTokens.FontSmall, UiTokens.Faint));
                row.AddChild(StudioStyles.Scope(session.FileScope));
            }

            return banner;
        }

        private void OnBreakpointToggled(StudioCodeLine line) => BreakpointToggled?.Invoke(line);

        private void OnMarkerDragged(Vector2 at)
        {
            _dragging = true;
            var target = DropTargetAt(at);
            if (target == _dropTarget)
                return;
            _dropTarget?.ShowDropTarget(false);
            _dropTarget = target;
            _dropTarget?.ShowDropTarget(true);
        }

        private void OnMarkerDropped(Vector2 at)
        {
            _dragging = false;
            _dropTarget?.ShowDropTarget(false);
            _dropTarget = null;
            var target = DropTargetAt(at);
            if (target != null)
                NextLineRequested?.Invoke(target.Line);
        }

        /// <summary>The row under <paramref name="at"/> (canvas coordinates) the dialog can continue from, or null.</summary>
        private StudioCodeLineView DropTargetAt(Vector2 at)
        {
            foreach (var view in _views)
            {
                if (view.Line.CanRunNext && view.GetGlobalRect().HasPoint(at))
                    return view;
            }
            return null;
        }

        private void OnAddLine() => InsertRequested?.Invoke(StudioLineKind.Line);

        private void OnAddChoice() => InsertRequested?.Invoke(StudioLineKind.Choice);

        private void OnAddJump() => InsertRequested?.Invoke(StudioLineKind.Jump);
    }
}
