using Godot;

namespace Cthangover.Core.UI.Theming
{
    /// <summary>
    /// Runtime-baked Godot <see cref="Theme"/> for the whole game UI. Built once
    /// on first access and assigned to a Control root, from which it cascades to
    /// every descendant that does not set its own explicit overrides. Covers
    /// standard controls (buttons, inputs, popups, sliders, scrollbars, tabs,
    /// lists, windows, tooltips) so panels stop relying on the default Godot
    /// skin. Colours come exclusively from <see cref="UiTokens"/>.
    /// <para/>
    /// Type variations registered here: <see cref="PrimaryVariation"/>,
    /// <see cref="DangerVariation"/>, <see cref="IconVariation"/>,
    /// <see cref="TabVariation"/>, <see cref="RibbonVariation"/>. Use the
    /// <c>Apply*</c> helpers to opt a control into one.
    /// </summary>
    public static class CthangoverTheme
    {
        /// <summary>Name of the gold primary button variation registered by <see cref="Build"/>.</summary>
        public const string PrimaryVariation = "CthangoverPrimary";
        /// <summary>Name of the destructive red-accent button variation registered by <see cref="Build"/>.</summary>
        public const string DangerVariation = "CthangoverDanger";
        /// <summary>Name of the compact icon-button variation registered by <see cref="Build"/>.</summary>
        public const string IconVariation = "CthangoverIcon";
        /// <summary>Name of the tab-shaped button variation registered by <see cref="Build"/>.</summary>
        public const string TabVariation = "CthangoverTab";
        /// <summary>Name of the ribbon-tab button variation registered by <see cref="Build"/>.</summary>
        public const string RibbonVariation = "CthangoverRibbon";

        private static Theme instance;

        /// <summary>Shared theme instance, built lazily at runtime.</summary>
        public static Theme Get() => instance ??= Build();

        /// <summary>Assigns the shared theme to a Control or Window so it cascades to children.</summary>
        public static void Apply(Node node)
        {
            switch (node)
            {
                case Control control:
                    control.Theme = Get();
                    break;
                case Window window:
                    window.Theme = Get();
                    break;
            }
        }

        /// <summary>Opts a button into the primary gold variation.</summary>
        public static void ApplyPrimary(Button button)
        {
            if (button != null)
                button.ThemeTypeVariation = PrimaryVariation;
        }

        /// <summary>Opts a button into the danger variation.</summary>
        public static void ApplyDanger(Button button)
        {
            if (button != null)
                button.ThemeTypeVariation = DangerVariation;
        }

        /// <summary>Opts a button into the compact icon-button variation (close ✕ buttons and glyph buttons).</summary>
        public static void ApplyIcon(Button button)
        {
            if (button != null)
                button.ThemeTypeVariation = IconVariation;
        }

        /// <summary>Opts a button into the tab variation.</summary>
        public static void ApplyTab(Button button)
        {
            if (button != null)
                button.ThemeTypeVariation = TabVariation;
        }

        /// <summary>Opts a button into the ribbon-tab variation.</summary>
        public static void ApplyRibbon(Button button)
        {
            if (button != null)
                button.ThemeTypeVariation = RibbonVariation;
        }

        /// <summary>Builds a fresh theme; prefer <see cref="Get"/> in runtime code.</summary>
        public static Theme Build()
        {
            var theme = new Theme { DefaultFontSize = UiTokens.FontBodyLarge };

            ConfigureGlobals(theme);
            ConfigureButtons(theme);
            ConfigureInputs(theme);
            ConfigurePopups(theme);
            ConfigureSliders(theme);
            ConfigureProgress(theme);
            ConfigureScrollbars(theme);
            ConfigureTabs(theme);
            ConfigureLists(theme);
            ConfigureWindows(theme);
            ConfigureMisc(theme);

            return theme;
        }

        // ── Globals ─────────────────────────────────────────────────────

        private static void ConfigureGlobals(Theme theme)
        {
            theme.SetColor("font_color", "Label", UiTokens.Ink);
            theme.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.6f));
            theme.SetFontSize("font_size", "Label", UiTokens.FontBodyLarge);

            theme.SetColor("default_color", "RichTextLabel", UiTokens.Ink);
            theme.SetFontSize("normal_font_size", "RichTextLabel", UiTokens.FontLead);
            theme.SetFontSize("italics_font_size", "RichTextLabel", UiTokens.FontLead);
            theme.SetFontSize("bold_font_size", "RichTextLabel", UiTokens.FontLead);
            theme.SetFontSize("mono_font_size", "RichTextLabel", UiTokens.FontBody);
            theme.SetConstant("line_separation", "RichTextLabel", 7);
            theme.SetStylebox("normal", "RichTextLabel", Flat(0, new Color(0f, 0f, 0f, 0f)));
            theme.SetStylebox("focus", "RichTextLabel", new StyleBoxEmpty());

            theme.SetStylebox("panel", "Panel", WindowBox());
            theme.SetStylebox("panel", "PanelContainer", WindowBox());
        }

        // ── Buttons ─────────────────────────────────────────────────────

        private static void ConfigureButtons(Theme theme)
        {
            var normal = Flat(UiTokens.RadiusSm, UiTokens.Surface3, UiTokens.Line);
            normal.ContentMarginLeft = 18;
            normal.ContentMarginRight = 18;
            normal.ContentMarginTop = 11;
            normal.ContentMarginBottom = 11;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.BtnHover, UiTokens.AccentLine);
            hover.ContentMarginLeft = 18;
            hover.ContentMarginRight = 18;
            hover.ContentMarginTop = 11;
            hover.ContentMarginBottom = 11;

            var pressed = Flat(UiTokens.RadiusSm, UiTokens.BtnHover, UiTokens.AccentLineStrong);
            pressed.ContentMarginLeft = 18;
            pressed.ContentMarginRight = 18;
            pressed.ContentMarginTop = 11;
            pressed.ContentMarginBottom = 11;

            var disabled = Flat(UiTokens.RadiusSm, UiTokens.BtnDisabled, UiTokens.LineDisabled);
            disabled.ContentMarginLeft = 18;
            disabled.ContentMarginRight = 18;
            disabled.ContentMarginTop = 11;
            disabled.ContentMarginBottom = 11;

            SetButtonStyles(theme, "Button", normal, hover, pressed, disabled);
            theme.SetColor("font_color", "Button", UiTokens.Body);
            theme.SetColor("font_hover_color", "Button", UiTokens.InkBright);
            theme.SetColor("font_pressed_color", "Button", UiTokens.AccentBright);
            theme.SetColor("font_focus_color", "Button", UiTokens.InkBright);
            theme.SetColor("font_disabled_color", "Button", UiTokens.Faint);
            theme.SetFontSize("font_size", "Button", UiTokens.FontBodyLarge);

            SetButtonStyles(theme, "OptionButton", normal, hover, pressed, disabled);
            theme.SetColor("font_color", "OptionButton", UiTokens.Body);
            theme.SetColor("font_hover_color", "OptionButton", UiTokens.InkBright);
            theme.SetColor("font_pressed_color", "OptionButton", UiTokens.AccentBright);
            theme.SetColor("font_disabled_color", "OptionButton", UiTokens.Faint);
            theme.SetFontSize("font_size", "OptionButton", UiTokens.FontBody);
            theme.SetIcon("arrow", "OptionButton", MakeArrowTexture());

            theme.SetColor("font_color", "MenuButton", UiTokens.Body);
            theme.SetColor("font_hover_color", "MenuButton", UiTokens.InkBright);
            theme.SetColor("font_pressed_color", "MenuButton", UiTokens.AccentBright);
            theme.SetColor("font_disabled_color", "MenuButton", UiTokens.Faint);
            theme.SetFontSize("font_size", "MenuButton", UiTokens.FontBodyLarge);

            theme.SetColor("font_color", "CheckBox", UiTokens.Body);
            theme.SetColor("font_hover_color", "CheckBox", UiTokens.InkBright);
            theme.SetColor("font_pressed_color", "CheckBox", UiTokens.AccentBright);
            theme.SetColor("font_disabled_color", "CheckBox", UiTokens.Faint);
            theme.SetFontSize("font_size", "CheckBox", UiTokens.FontBodyLarge);

            theme.SetColor("font_color", "CheckButton", UiTokens.Body);
            theme.SetColor("font_hover_color", "CheckButton", UiTokens.InkBright);
            theme.SetColor("font_pressed_color", "CheckButton", UiTokens.AccentBright);
            theme.SetColor("font_disabled_color", "CheckButton", UiTokens.Faint);
            theme.SetFontSize("font_size", "CheckButton", UiTokens.FontBodyLarge);
            theme.SetColor("font_focus_color", "CheckButton", UiTokens.InkBright);
            theme.SetColor("font_hover_pressed_color", "CheckButton", UiTokens.AccentBright);
            ToggleSwitchTextures.Apply(theme);

            BuildPrimaryVariation(theme);
            BuildDangerVariation(theme);
            BuildIconVariation(theme);
            BuildTabVariation(theme);
            BuildRibbonVariation(theme);
        }

        private static void SetButtonStyles(Theme theme, string type, StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled)
        {
            theme.SetStylebox("normal", type, normal);
            theme.SetStylebox("hover", type, hover);
            theme.SetStylebox("pressed", type, pressed);
            theme.SetStylebox("disabled", type, disabled);
            theme.SetStylebox("focus", type, new StyleBoxEmpty());
        }

        private static void BuildPrimaryVariation(Theme theme)
        {
            theme.SetTypeVariation(PrimaryVariation, "Button");

            var normal = Flat(UiTokens.RadiusSm, UiTokens.BtnPrimary, UiTokens.AccentLine);
            normal.ContentMarginLeft = 18;
            normal.ContentMarginRight = 18;
            normal.ContentMarginTop = 11;
            normal.ContentMarginBottom = 11;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.BtnPrimaryHover, UiTokens.AccentLineStrong);
            hover.ContentMarginLeft = 18;
            hover.ContentMarginRight = 18;
            hover.ContentMarginTop = 11;
            hover.ContentMarginBottom = 11;

            var pressed = Flat(UiTokens.RadiusSm, UiTokens.BtnPrimaryHover, UiTokens.Accent);
            pressed.ContentMarginLeft = 18;
            pressed.ContentMarginRight = 18;
            pressed.ContentMarginTop = 11;
            pressed.ContentMarginBottom = 11;

            theme.SetStylebox("normal", PrimaryVariation, normal);
            theme.SetStylebox("hover", PrimaryVariation, hover);
            theme.SetStylebox("pressed", PrimaryVariation, pressed);
            theme.SetColor("font_color", PrimaryVariation, UiTokens.AccentBright);
            theme.SetColor("font_hover_color", PrimaryVariation, UiTokens.AccentHover);
            theme.SetColor("font_pressed_color", PrimaryVariation, UiTokens.AccentHover);
            theme.SetColor("font_disabled_color", PrimaryVariation, UiTokens.Faint);
        }

        private static void BuildDangerVariation(Theme theme)
        {
            theme.SetTypeVariation(DangerVariation, "Button");

            var normal = Flat(UiTokens.RadiusSm, UiTokens.Surface3, UiTokens.Line);
            normal.ContentMarginLeft = 18;
            normal.ContentMarginRight = 18;
            normal.ContentMarginTop = 11;
            normal.ContentMarginBottom = 11;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.DangerSoft, UiTokens.Danger);
            hover.ContentMarginLeft = 18;
            hover.ContentMarginRight = 18;
            hover.ContentMarginTop = 11;
            hover.ContentMarginBottom = 11;

            theme.SetStylebox("normal", DangerVariation, normal);
            theme.SetStylebox("hover", DangerVariation, hover);
            theme.SetStylebox("pressed", DangerVariation, hover);
            theme.SetColor("font_color", DangerVariation, UiTokens.Body);
            theme.SetColor("font_hover_color", DangerVariation, UiTokens.DangerText);
            theme.SetColor("font_pressed_color", DangerVariation, UiTokens.DangerText);
            theme.SetColor("font_disabled_color", DangerVariation, UiTokens.Faint);
        }

        private static void BuildIconVariation(Theme theme)
        {
            theme.SetTypeVariation(IconVariation, "Button");

            var normal = Flat(UiTokens.RadiusSm, new Color(0f, 0f, 0f, 0f), UiTokens.Line);
            normal.ContentMarginLeft = 6;
            normal.ContentMarginRight = 6;
            normal.ContentMarginTop = 6;
            normal.ContentMarginBottom = 6;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.DangerSoft, UiTokens.Danger);
            hover.ContentMarginLeft = 6;
            hover.ContentMarginRight = 6;
            hover.ContentMarginTop = 6;
            hover.ContentMarginBottom = 6;

            theme.SetStylebox("normal", IconVariation, normal);
            theme.SetStylebox("hover", IconVariation, hover);
            theme.SetStylebox("pressed", IconVariation, hover);
            theme.SetColor("font_color", IconVariation, UiTokens.Muted);
            theme.SetColor("font_hover_color", IconVariation, UiTokens.White);
            theme.SetColor("font_pressed_color", IconVariation, UiTokens.White);
            theme.SetFontSize("font_size", IconVariation, UiTokens.FontLead);
        }

        private static void BuildTabVariation(Theme theme)
        {
            theme.SetTypeVariation(TabVariation, "Button");

            var normal = Flat(UiTokens.RadiusSm, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            normal.ContentMarginLeft = 13;
            normal.ContentMarginRight = 13;
            normal.ContentMarginTop = 7;
            normal.ContentMarginBottom = 7;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.WhiteTrace, new Color(0f, 0f, 0f, 0f));
            hover.ContentMarginLeft = 13;
            hover.ContentMarginRight = 13;
            hover.ContentMarginTop = 7;
            hover.ContentMarginBottom = 7;

            var active = Flat(UiTokens.RadiusSm, UiTokens.AccentSoft, UiTokens.AccentLine);
            active.ContentMarginLeft = 13;
            active.ContentMarginRight = 13;
            active.ContentMarginTop = 7;
            active.ContentMarginBottom = 7;

            theme.SetStylebox("normal", TabVariation, normal);
            theme.SetStylebox("hover", TabVariation, hover);
            theme.SetStylebox("pressed", TabVariation, active);
            theme.SetStylebox("focus", TabVariation, new StyleBoxEmpty());
            theme.SetColor("font_color", TabVariation, UiTokens.Muted);
            theme.SetColor("font_hover_color", TabVariation, UiTokens.Ink);
            theme.SetColor("font_pressed_color", TabVariation, UiTokens.AccentBright);
            theme.SetFontSize("font_size", TabVariation, UiTokens.FontBody);
        }

        private static void BuildRibbonVariation(Theme theme)
        {
            theme.SetTypeVariation(RibbonVariation, "Button");

            var normal = Flat(UiTokens.RadiusSm, UiTokens.Ribbon, UiTokens.PaperLine);
            normal.CornerRadiusBottomLeft = 0;
            normal.CornerRadiusBottomRight = 0;
            normal.BorderWidthBottom = 0;
            normal.ContentMarginLeft = 22;
            normal.ContentMarginRight = 22;
            normal.ContentMarginTop = 12;
            normal.ContentMarginBottom = 20;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.RibbonHover, UiTokens.PaperLine);
            hover.CornerRadiusBottomLeft = 0;
            hover.CornerRadiusBottomRight = 0;
            hover.BorderWidthBottom = 0;
            hover.ContentMarginLeft = 22;
            hover.ContentMarginRight = 22;
            hover.ContentMarginTop = 12;
            hover.ContentMarginBottom = 20;

            var active = Flat(UiTokens.RadiusSm, UiTokens.Accent, UiTokens.Accent);
            active.CornerRadiusBottomLeft = 0;
            active.CornerRadiusBottomRight = 0;
            active.BorderWidthBottom = 0;
            active.ContentMarginLeft = 22;
            active.ContentMarginRight = 22;
            active.ContentMarginTop = 12;
            active.ContentMarginBottom = 20;

            theme.SetStylebox("normal", RibbonVariation, normal);
            theme.SetStylebox("hover", RibbonVariation, hover);
            theme.SetStylebox("pressed", RibbonVariation, active);
            theme.SetStylebox("focus", RibbonVariation, new StyleBoxEmpty());
            theme.SetColor("font_color", RibbonVariation, UiTokens.RibbonText);
            theme.SetColor("font_hover_color", RibbonVariation, UiTokens.InkBright);
            theme.SetColor("font_pressed_color", RibbonVariation, UiTokens.RibbonTextActive);
            theme.SetFontSize("font_size", RibbonVariation, 15);
        }

        // ── Inputs ──────────────────────────────────────────────────────

        private static void ConfigureInputs(Theme theme)
        {
            foreach (var type in new[] { "LineEdit", "TextEdit" })
            {
                var normal = Flat(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line);
                normal.ContentMarginLeft = 12;
                normal.ContentMarginRight = 12;
                normal.ContentMarginTop = 9;
                normal.ContentMarginBottom = 9;

                var focus = Flat(UiTokens.RadiusSm, UiTokens.Well, UiTokens.AccentLine);
                focus.ContentMarginLeft = 12;
                focus.ContentMarginRight = 12;
                focus.ContentMarginTop = 9;
                focus.ContentMarginBottom = 9;

                theme.SetStylebox("normal", type, normal);
                theme.SetStylebox("focus", type, focus);
                theme.SetStylebox("read_only", type, normal);
                theme.SetColor("font_color", type, UiTokens.Ink);
                theme.SetColor("font_placeholder_color", type, UiTokens.Faint);
                theme.SetColor("font_selected_color", type, UiTokens.InkBright);
                theme.SetColor("caret_color", type, UiTokens.Accent);
                theme.SetColor("selection_color", type, UiTokens.AccentSoft);
                theme.SetColor("clear_button_color", type, UiTokens.Muted);
                theme.SetColor("clear_button_color_pressed", type, UiTokens.AccentBright);
                theme.SetFontSize("font_size", type, UiTokens.FontBody);
            }
        }

        private static void ConfigurePopups(Theme theme)
        {
            var panel = Flat(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
            panel.ContentMarginLeft = 4;
            panel.ContentMarginRight = 4;
            panel.ContentMarginTop = 4;
            panel.ContentMarginBottom = 4;

            var hover = Flat(UiTokens.RadiusSm, UiTokens.AccentTrace, new Color(0f, 0f, 0f, 0f));
            hover.ContentMarginLeft = 8;
            hover.ContentMarginRight = 8;
            hover.ContentMarginTop = 4;
            hover.ContentMarginBottom = 4;

            theme.SetStylebox("panel", "PopupMenu", panel);
            theme.SetStylebox("hover", "PopupMenu", hover);
            theme.SetColor("font_color", "PopupMenu", UiTokens.Body);
            theme.SetColor("font_hover_color", "PopupMenu", UiTokens.AccentBright);
            theme.SetColor("font_disabled_color", "PopupMenu", UiTokens.Faint);
            theme.SetColor("font_accelerator_color", "PopupMenu", UiTokens.Faint);
            theme.SetColor("font_separator_color", "PopupMenu", UiTokens.Muted);
            theme.SetFontSize("font_size", "PopupMenu", UiTokens.FontBody);
            theme.SetConstant("v_separation", "PopupMenu", 6);
            theme.SetConstant("item_start_padding", "PopupMenu", 8);
            theme.SetConstant("item_end_padding", "PopupMenu", 8);

            theme.SetStylebox("panel", "PopupPanel", panel);
        }

        // ── Sliders ─────────────────────────────────────────────────────

        private static void ConfigureSliders(Theme theme)
        {
            var track = Flat(3, UiTokens.LineDisabled, new Color(0f, 0f, 0f, 0f));
            track.ContentMarginTop = 2;
            track.ContentMarginBottom = 2;

            var fill = Flat(3, UiTokens.Accent, new Color(0f, 0f, 0f, 0f));
            fill.ContentMarginTop = 2;
            fill.ContentMarginBottom = 2;

            var fillHighlight = Flat(3, UiTokens.AccentBright, new Color(0f, 0f, 0f, 0f));
            fillHighlight.ContentMarginTop = 2;
            fillHighlight.ContentMarginBottom = 2;

            foreach (var type in new[] { "HSlider", "VSlider" })
            {
                theme.SetStylebox("slider", type, track);
                theme.SetStylebox("grabber_area", type, fill);
                theme.SetStylebox("grabber_area_highlight", type, fillHighlight);

                theme.SetIcon("grabber", type, MakeCircleTexture(14, UiTokens.AccentBright, UiTokens.Accent));
                theme.SetIcon("grabber_highlight", type, MakeCircleTexture(14, UiTokens.AccentHover, UiTokens.AccentBright));
                theme.SetIcon("grabber_disabled", type, MakeCircleTexture(14, UiTokens.LineStrong, UiTokens.LineFaint));

                theme.SetConstant("center_grabber", type, 0);
                theme.SetConstant("grabber_offset", type, 0);
            }
        }

        // ── Progress ────────────────────────────────────────────────────

        private static void ConfigureProgress(Theme theme)
        {
            var background = Flat(UiTokens.RadiusSm, UiTokens.LineDisabled, new Color(0f, 0f, 0f, 0f));
            var fill = Flat(UiTokens.RadiusSm, UiTokens.HpOk, new Color(0f, 0f, 0f, 0f));

            theme.SetStylebox("background", "ProgressBar", background);
            theme.SetStylebox("fill", "ProgressBar", fill);
            theme.SetColor("font_color", "ProgressBar", UiTokens.Muted);
            theme.SetFontSize("font_size", "ProgressBar", UiTokens.FontBodySmall);
        }

        // ── Scrollbars ──────────────────────────────────────────────────

        private static void ConfigureScrollbars(Theme theme)
        {
            var track = Flat(0, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            var grabber = Flat(5, UiTokens.ScrollThumb, new Color(0f, 0f, 0f, 0f));
            grabber.ContentMarginLeft = 2;
            grabber.ContentMarginRight = 2;
            grabber.ContentMarginTop = 2;
            grabber.ContentMarginBottom = 2;

            var grabberHover = Flat(5, UiTokens.LineStrong, new Color(0f, 0f, 0f, 0f));
            grabberHover.ContentMarginLeft = 2;
            grabberHover.ContentMarginRight = 2;
            grabberHover.ContentMarginTop = 2;
            grabberHover.ContentMarginBottom = 2;

            foreach (var type in new[] { "VScrollBar", "HScrollBar" })
            {
                theme.SetStylebox("scroll", type, track);
                theme.SetStylebox("grabber", type, grabber);
                theme.SetStylebox("grabber_highlight", type, grabberHover);
                theme.SetStylebox("grabber_pressed", type, grabberHover);
            }

            theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
        }

        // ── Tabs ────────────────────────────────────────────────────────

        private static void ConfigureTabs(Theme theme)
        {
            var unselected = Flat(UiTokens.RadiusSm, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            unselected.ContentMarginLeft = 13;
            unselected.ContentMarginRight = 13;
            unselected.ContentMarginTop = 7;
            unselected.ContentMarginBottom = 7;

            var hovered = Flat(UiTokens.RadiusSm, UiTokens.WhiteTrace, new Color(0f, 0f, 0f, 0f));
            hovered.ContentMarginLeft = 13;
            hovered.ContentMarginRight = 13;
            hovered.ContentMarginTop = 7;
            hovered.ContentMarginBottom = 7;

            var selected = Flat(UiTokens.RadiusSm, UiTokens.AccentSoft, UiTokens.AccentLine);
            selected.ContentMarginLeft = 13;
            selected.ContentMarginRight = 13;
            selected.ContentMarginTop = 7;
            selected.ContentMarginBottom = 7;

            foreach (var type in new[] { "TabBar", "TabContainer" })
            {
                theme.SetStylebox("tab_unselected", type, unselected);
                theme.SetStylebox("tab_hovered", type, hovered);
                theme.SetStylebox("tab_selected", type, selected);
                theme.SetStylebox("tab_disabled", type, unselected);
                theme.SetStylebox("tab_focus", type, new StyleBoxEmpty());
                theme.SetColor("font_unselected_color", type, UiTokens.Muted);
                theme.SetColor("font_hovered_color", type, UiTokens.Ink);
                theme.SetColor("font_selected_color", type, UiTokens.AccentBright);
                theme.SetColor("font_disabled_color", type, UiTokens.Faint);
                theme.SetFontSize("font_size", type, UiTokens.FontBody);
            }

            theme.SetStylebox("panel", "TabContainer", WindowBox());
            theme.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());
        }

        // ── Lists ───────────────────────────────────────────────────────

        private static void ConfigureLists(Theme theme)
        {
            var panel = Flat(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
            panel.ContentMarginLeft = 4;
            panel.ContentMarginRight = 4;
            panel.ContentMarginTop = 4;
            panel.ContentMarginBottom = 4;

            var selected = Flat(UiTokens.RadiusSm, UiTokens.AccentTrace, new Color(0f, 0f, 0f, 0f));
            var cursor = Flat(UiTokens.RadiusSm, UiTokens.AccentTrace, new Color(0f, 0f, 0f, 0f));

            theme.SetStylebox("panel", "ItemList", panel);
            theme.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
            theme.SetStylebox("selected", "ItemList", selected);
            theme.SetStylebox("selected_focus", "ItemList", selected);
            theme.SetStylebox("cursor", "ItemList", cursor);
            theme.SetStylebox("cursor_unfocused", "ItemList", cursor);
            theme.SetColor("font_color", "ItemList", UiTokens.Ink);
            theme.SetColor("font_selected_color", "ItemList", UiTokens.AccentBright);
            theme.SetColor("font_focus_color", "ItemList", UiTokens.InkBright);
            theme.SetFontSize("font_size", "ItemList", UiTokens.FontBody);

            theme.SetStylebox("panel", "Tree", panel);
            theme.SetStylebox("focus", "Tree", new StyleBoxEmpty());
            theme.SetStylebox("selected", "Tree", selected);
            theme.SetStylebox("selected_focus", "Tree", selected);
            theme.SetStylebox("cursor", "Tree", cursor);
            theme.SetStylebox("cursor_unfocused", "Tree", cursor);
            theme.SetColor("font_color", "Tree", UiTokens.Ink);
            theme.SetColor("font_selected_color", "Tree", UiTokens.AccentBright);
            theme.SetColor("font_focus_color", "Tree", UiTokens.InkBright);
            theme.SetColor("guide_color", "Tree", UiTokens.Line);
            theme.SetFontSize("font_size", "Tree", UiTokens.FontBody);
        }

        // ── Windows and tooltips ────────────────────────────────────────

        private static void ConfigureWindows(Theme theme)
        {
            theme.SetStylebox("panel", "Window", WindowBox());
            theme.SetStylebox("embedded_border", "Window", WindowBox());
            theme.SetStylebox("embedded_unfocused_border", "Window", WindowBox());
            theme.SetColor("title_color", "Window", UiTokens.InkBright);
            theme.SetFontSize("title_font_size", "Window", UiTokens.FontLead);
            theme.SetConstant("title_height", "Window", 36);
            theme.SetConstant("close_h_offset", "Window", 16);
            theme.SetConstant("close_v_offset", "Window", 8);

            var tooltip = Flat(UiTokens.RadiusSm, UiTokens.TooltipBg, UiTokens.Line);
            tooltip.ContentMarginLeft = 10;
            tooltip.ContentMarginRight = 10;
            tooltip.ContentMarginTop = 8;
            tooltip.ContentMarginBottom = 8;
            theme.SetStylebox("panel", "TooltipPanel", tooltip);
            theme.SetColor("font_color", "TooltipLabel", UiTokens.Body);
            theme.SetFontSize("font_size", "TooltipLabel", UiTokens.FontBodySmall);
        }

        // ── Misc ────────────────────────────────────────────────────────

        private static void ConfigureMisc(Theme theme)
        {
            var separator = new StyleBoxLine { Color = UiTokens.Line, Thickness = 1 };
            theme.SetStylebox("separator", "HSeparator", separator);
            theme.SetStylebox("separator", "VSeparator", separator);

            theme.SetColor("font_color", "SpinBox", UiTokens.Ink);
            theme.SetFontSize("font_size", "SpinBox", UiTokens.FontBody);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static StyleBoxFlat Flat(int radius, Color background, Color? border = null)
        {
            var style = new StyleBoxFlat
            {
                BgColor = background,
                CornerRadiusTopLeft = radius,
                CornerRadiusTopRight = radius,
                CornerRadiusBottomLeft = radius,
                CornerRadiusBottomRight = radius,
            };

            if (border.HasValue && border.Value.A > 0f)
            {
                style.BorderColor = border.Value;
                style.BorderWidthLeft = 1;
                style.BorderWidthRight = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthBottom = 1;
            }

            return style;
        }

        private static StyleBoxFlat WindowBox()
        {
            var style = Flat(UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line);
            style.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
            style.ShadowSize = 18;
            style.ShadowOffset = new Vector2(0, 8);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 16;
            style.ContentMarginBottom = 16;
            return style;
        }

        private static ImageTexture MakeCircleTexture(int size, Color fill, Color border)
        {
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0f, 0f, 0f, 0f));

            var center = (size - 1) / 2f;
            var radius = size / 2f - 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance <= radius - 1.1f)
                        image.SetPixel(x, y, fill);
                    else if (distance <= radius)
                        image.SetPixel(x, y, border);
                }
            }

            return ImageTexture.CreateFromImage(image);
        }

        private static ImageTexture MakeArrowTexture()
        {
            const int width = 11;
            const int height = 6;
            var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            image.Fill(new Color(0f, 0f, 0f, 0f));

            for (var y = 0; y < height; y++)
            {
                var run = height - y;
                var start = (width - run) / 2;
                for (var x = start; x < start + run; x++)
                    image.SetPixel(x, y, UiTokens.Body);
            }

            return ImageTexture.CreateFromImage(image);
        }
    }
}
