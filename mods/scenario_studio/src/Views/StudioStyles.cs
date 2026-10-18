namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Interface.Tool;
    using Godot;

    /// <summary>
    /// Godot counterparts of the studio components of the style guide
    /// (<c>mockups/style_guide</c>, section <c>#studio</c>: <c>.ct-dock</c>,
    /// <c>.ct-scope</c>, <c>.ct-code</c>, <c>.ct-block</c>, <c>.ct-cand</c>…).
    /// Every colour comes from <see cref="UiTokens"/>; mod scenes ignore theme
    /// overrides, so the dock skeleton from the scene is dressed here and the
    /// data rows are built with the same helpers.
    /// <para/>
    /// Two deliberate deviations from the HTML guide, both recorded there:
    /// <c>StyleBoxFlat</c> has no dashed border, so the insert bar and new map
    /// nodes use a solid low-alpha border instead of a dashed one.
    /// </summary>
    public static class StudioStyles
    {
        /// <summary>Width of the dock; the scene offsets use the same value plus the 8px edge gap.</summary>
        public const int DockWidth = 480;

        public static Font Mono => ToolWindow.GetMonospaceFont();

        /// <summary>Dock surface: window surface, line border, large radius and the window shadow, without any backdrop.</summary>
        public static StyleBoxFlat Dock()
        {
            var style = UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line);
            style.ShadowColor = UiTokens.Alpha(Colors.Black, 0.6f);
            style.ShadowSize = 24;
            style.ShadowOffset = new Vector2(0, 12);
            return style;
        }

        public static Label Title(Label label, string text)
        {
            label.Text = text.ToUpperInvariant();
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontLead);
            label.AddThemeColorOverride("font_color", UiTokens.InkBright);
            return label;
        }

        /// <summary>Monospace state plate: running is green, paused is warning yellow.</summary>
        public static void RunState(PanelContainer plate, Label text, bool paused, string caption)
        {
            var color = paused ? UiTokens.Warning : UiTokens.Success;
            plate.AddThemeStyleboxOverride("panel", Chip(UiTokens.BadgeFill(color), UiTokens.BadgeLine(color)));
            text.Text = caption.ToUpperInvariant();
            text.AddThemeFontOverride("font", Mono);
            text.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
            text.AddThemeColorOverride("font_color", color);
        }

        /// <summary>Well behind the segmented mode switch.</summary>
        public static StyleBoxFlat SegmentWell()
        {
            var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line);
            Pad(style, 2, 2);
            return style;
        }

        /// <summary>Segment of the mode switch: transparent, the pressed one looks like the active tab.</summary>
        public static void Segment(Button button, string text)
        {
            button.Text = text;
            var normal = UiKit.Frame(UiTokens.RadiusXs, Colors.Transparent);
            Pad(normal, 10, 3);
            var hover = UiKit.Frame(UiTokens.RadiusXs, UiTokens.WhiteTrace);
            Pad(hover, 10, 3);
            var active = UiKit.Frame(UiTokens.RadiusXs, UiTokens.AccentSoft, UiTokens.AccentLine);
            Pad(active, 10, 3);
            ApplyStates(button, normal, hover, active);
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            button.AddThemeColorOverride("font_color", UiTokens.Muted);
            button.AddThemeColorOverride("font_hover_color", UiTokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", UiTokens.AccentBright);
            button.AddThemeColorOverride("font_hover_pressed_color", UiTokens.AccentBright);
        }

        /// <summary>Transport button (pause/step/continue/restart): a compact secondary button; pressed means "on".</summary>
        public static void Transport(Button button, string text)
        {
            button.Text = text;
            var normal = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Surface3, UiTokens.Line);
            Pad(normal, 10, 5);
            var hover = UiKit.Frame(UiTokens.RadiusSm, UiTokens.BtnHover, UiTokens.AccentLine);
            Pad(hover, 10, 5);
            var on = UiKit.Frame(UiTokens.RadiusSm, UiTokens.BtnPrimary, UiTokens.AccentLine);
            Pad(on, 10, 5);
            ApplyStates(button, normal, hover, on);
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            button.AddThemeColorOverride("font_color", UiTokens.Body);
            button.AddThemeColorOverride("font_hover_color", UiTokens.InkBright);
            button.AddThemeColorOverride("font_pressed_color", UiTokens.AccentBright);
            button.AddThemeColorOverride("font_hover_pressed_color", UiTokens.AccentBright);
        }

        /// <summary>Small tab (<c>.ct-tab--sm</c>): the tab variation with the compact paddings.</summary>
        public static void SmallTab(Button button, string text)
        {
            button.Text = text;
            var normal = UiKit.Frame(UiTokens.RadiusXs, Colors.Transparent);
            Pad(normal, 9, 4);
            var hover = UiKit.Frame(UiTokens.RadiusXs, UiTokens.WhiteTrace);
            Pad(hover, 9, 4);
            var active = UiKit.Frame(UiTokens.RadiusXs, UiTokens.AccentSoft, UiTokens.AccentLine);
            Pad(active, 9, 4);
            ApplyStates(button, normal, hover, active);
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            button.AddThemeColorOverride("font_color", UiTokens.Muted);
            button.AddThemeColorOverride("font_hover_color", UiTokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", UiTokens.AccentBright);
            button.AddThemeColorOverride("font_hover_pressed_color", UiTokens.AccentBright);
        }

        /// <summary>Small action button inside the body (<c>.ct-btn--sm</c>); primary turns it gold.</summary>
        public static Button SmallButton(Button button, string text, bool primary = false)
        {
            button.Text = text;
            var background = primary ? UiTokens.BtnPrimary : UiTokens.Surface3;
            var border = primary ? UiTokens.AccentLine : UiTokens.Line;
            var normal = UiKit.Frame(UiTokens.RadiusSm, background, border);
            Pad(normal, 12, 6);
            var hover = UiKit.Frame(UiTokens.RadiusSm, primary ? UiTokens.BtnPrimaryHover : UiTokens.BtnHover, primary ? UiTokens.AccentLineStrong : UiTokens.AccentLine);
            Pad(hover, 12, 6);
            ApplyStates(button, normal, hover, hover);
            button.FocusMode = Control.FocusModeEnum.None;
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            button.AddThemeColorOverride("font_color", primary ? UiTokens.AccentBright : UiTokens.Body);
            button.AddThemeColorOverride("font_hover_color", primary ? UiTokens.AccentHover : UiTokens.InkBright);
            button.AddThemeColorOverride("font_pressed_color", primary ? UiTokens.AccentHover : UiTokens.InkBright);
            return button;
        }

        public static Label Text(string text, int size, Color color, bool mono = false)
        {
            var label = UiKit.Label(text, size, color);
            if (mono)
                label.AddThemeFontOverride("font", Mono);
            return label;
        }

        /// <summary>
        /// Mod scope badge (<c>.ct-scope</c>): caps kind caption plus the mono mod id,
        /// coloured by how the studio may touch the mod.
        /// </summary>
        public static Control Scope(StudioModScope scope)
        {
            var color = ScopeColor(scope.Kind);
            var style = scope.Kind == StudioScopeKind.ReadOnly
                ? Chip(Colors.Transparent, UiTokens.Line)
                : Chip(UiTokens.BadgeFill(color), UiTokens.BadgeLine(color));

            var badge = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            badge.AddThemeStyleboxOverride("panel", style);

            var row = UiKit.Row(UiTokens.Space2);
            var kind = UiKit.Label(TranslationServer.Translate(ScopeKey(scope.Kind)), UiTokens.FontMicro, color);
            kind.Uppercase = true;
            kind.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(kind);
            row.AddChild(Text(scope.ModId, UiTokens.FontSmall, color, mono: true));
            badge.AddChild(row);
            return badge;
        }

        public static Color ScopeColor(StudioScopeKind kind)
        {
            switch (kind)
            {
                case StudioScopeKind.Own:
                    return UiTokens.SuccessText;
                case StudioScopeKind.Overlay:
                    return UiTokens.Info;
                case StudioScopeKind.Layer:
                    return UiTokens.TypeTarget;
                default:
                    return UiTokens.Faint;
            }
        }

        private static string ScopeKey(StudioScopeKind kind)
        {
            switch (kind)
            {
                case StudioScopeKind.Own:
                    return "studio/scope_own";
                case StudioScopeKind.Overlay:
                    return "studio/scope_overlay";
                case StudioScopeKind.Layer:
                    return "studio/scope_layer";
                default:
                    return "studio/scope_readonly";
            }
        }

        /// <summary>"This change affects" plate: warning-tinted, lists every mod the action writes to or overrides.</summary>
        public static StyleBoxFlat ScopeBanner()
        {
            var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.BadgeFill(UiTokens.Warning), UiTokens.BadgeLine(UiTokens.Warning));
            Pad(style, 12, 8);
            return style;
        }

        /// <summary>Well behind the script lines (<c>.ct-code</c>).</summary>
        public static StyleBoxFlat CodeWell()
        {
            var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line);
            Pad(style, 0, 4);
            return style;
        }

        /// <summary>Row of one script line: a 2px left bar and a tint carry current / new / error.</summary>
        public static StyleBoxFlat CodeLine(StudioCodeLine line)
        {
            Color background = Colors.Transparent;
            Color bar = Colors.Transparent;
            if (line.IsCurrent)
            {
                background = UiTokens.AccentSoft;
                bar = UiTokens.Accent;
            }
            else if (line.Error != null)
            {
                background = UiTokens.DangerSoft;
                bar = UiTokens.Danger;
            }
            else if (line.IsNew)
            {
                background = UiTokens.SuccessSoft;
                bar = UiTokens.Success;
            }

            var style = new StyleBoxFlat { BgColor = background, BorderColor = bar, BorderWidthLeft = 2 };
            Pad(style, 0, 1);
            style.ContentMarginRight = UiTokens.Space4;
            return style;
        }

        /// <summary>Breakpoint dot of the gutter; transparent when the line has none.</summary>
        public static StyleBoxFlat Breakpoint(bool on)
        {
            var style = UiKit.Frame(4, on ? UiTokens.Danger : Colors.Transparent);
            if (on)
            {
                style.ShadowColor = UiTokens.DangerSoft;
                style.ShadowSize = 2;
            }
            return style;
        }

        /// <summary>Novice card of a line (<c>.ct-block</c>); the current one looks like a selected slot.</summary>
        public static StyleBoxFlat Block(StudioCodeLine line)
        {
            var current = line.IsCurrent;
            var style = UiKit.Frame(UiTokens.RadiusMd, current ? UiTokens.SurfaceSelected : UiTokens.Surface2, current ? UiTokens.AccentLine : UiTokens.Line);
            style.BorderWidthLeft = 3;
            if (line.IsNew)
                style.BorderColor = UiTokens.Success;
            else if (line.Error != null)
                style.BorderColor = UiTokens.Danger;
            else if (current)
                style.BorderColor = UiTokens.Accent;
            else
                style.BorderColor = UiTokens.LineStrong;
            Pad(style, 12, 8);
            return style;
        }

        public static Color KindColor(StudioLineKind kind)
        {
            switch (kind)
            {
                case StudioLineKind.Light:
                case StudioLineKind.Background:
                    return UiTokens.TypeResource;
                case StudioLineKind.Choice:
                case StudioLineKind.Option:
                    return UiTokens.TypeRecipe;
                case StudioLineKind.Label:
                case StudioLineKind.Jump:
                    return UiTokens.TypeTarget;
                case StudioLineKind.Action:
                    return UiTokens.TypeUsed;
                case StudioLineKind.End:
                    return UiTokens.Muted;
                default:
                    return UiTokens.Accent;
            }
        }

        /// <summary>Insert bar (<c>.ct-insert</c>); solid accent 20% border because StyleBoxFlat cannot dash.</summary>
        public static StyleBoxFlat InsertBar()
        {
            var style = UiKit.Frame(UiTokens.RadiusSm, Colors.Transparent, UiTokens.AccentLineWeak);
            Pad(style, 8, 6);
            return style;
        }

        /// <summary>Scene candidate card (<c>.ct-cand</c>); the running one gets the accent border.</summary>
        public static StyleBoxFlat Candidate(bool running)
        {
            var style = UiKit.Frame(UiTokens.RadiusMd, running ? UiTokens.SurfaceSelected : UiTokens.Surface2, running ? UiTokens.AccentLine : UiTokens.Line);
            Pad(style, 12, 8);
            return style;
        }

        public static Color StatusColor(StudioCandidateStatus status)
        {
            switch (status)
            {
                case StudioCandidateStatus.Running:
                    return UiTokens.Success;
                case StudioCandidateStatus.Waiting:
                    return UiTokens.Warning;
                case StudioCandidateStatus.Skipped:
                    return UiTokens.TypeEnemy;
                case StudioCandidateStatus.Completed:
                    return UiTokens.TypeResource;
                default:
                    return UiTokens.Muted;
            }
        }

        public static Color LogColor(StudioLogCategory category)
        {
            switch (category)
            {
                case StudioLogCategory.Scene:
                    return UiTokens.TypeTarget;
                case StudioLogCategory.Skip:
                    return UiTokens.TypeUsed;
                case StudioLogCategory.Stop:
                    return UiTokens.TypeEnemy;
                case StudioLogCategory.Choice:
                    return UiTokens.TypeRecipe;
                case StudioLogCategory.Edit:
                    return UiTokens.Success;
                default:
                    return UiTokens.Accent;
            }
        }

        /// <summary>Map node card (<c>.ct-node</c>): here glows gold, visited is strong-lined, new is teal.</summary>
        public static StyleBoxFlat Node(StudioNodeState state)
        {
            StyleBoxFlat style;
            switch (state)
            {
                case StudioNodeState.Here:
                    style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.SurfaceSelected, UiTokens.AccentLineStrong);
                    style.ShadowColor = UiTokens.Alpha(UiTokens.Accent, 0.35f);
                    style.ShadowSize = 8;
                    break;
                case StudioNodeState.Visited:
                    style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.LineStrong);
                    break;
                case StudioNodeState.New:
                    style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.BadgeLine(UiTokens.TypeTarget));
                    break;
                default:
                    style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
                    break;
            }
            Pad(style, 12, 8);
            return style;
        }

        /// <summary>Rows of the state tab: inset well like the inventory quantity block.</summary>
        public static StyleBoxFlat Well()
        {
            var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
            Pad(style, 12, 10);
            return style;
        }

        /// <summary>Bottom hairline between log rows.</summary>
        public static StyleBoxFlat LogRow(bool warning)
        {
            var style = new StyleBoxFlat
            {
                BgColor = warning ? UiTokens.BadgeFill(UiTokens.Warning) : Colors.Transparent,
                BorderColor = UiTokens.LineSoft,
                BorderWidthBottom = 1,
            };
            Pad(style, 8, 5);
            return style;
        }

        private static StyleBoxFlat Chip(Color fill, Color border)
        {
            var style = UiKit.Frame(UiTokens.RadiusXs, fill, border);
            Pad(style, 7, 2);
            return style;
        }

        private static void ApplyStates(Button button, StyleBox normal, StyleBox hover, StyleBox pressed)
        {
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeStyleboxOverride("hover_pressed", pressed);
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        }

        private static void Pad(StyleBoxFlat style, int horizontal, int vertical)
        {
            style.ContentMarginLeft = horizontal;
            style.ContentMarginRight = horizontal;
            style.ContentMarginTop = vertical;
            style.ContentMarginBottom = vertical;
        }
    }
}
