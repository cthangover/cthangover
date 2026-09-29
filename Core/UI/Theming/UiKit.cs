using Cthangover.Core.Mods.Resolvers;
using Godot;

namespace Cthangover.Core.UI.Theming
{
    public enum ButtonKind
    {
        Secondary,
        Primary,
        Danger,
        Icon,
        Tab,
    }

    /// <summary>
    /// Runtime factories for composed UI pieces that are not standard Godot
    /// controls (badges, chips, stat chips, empty states) plus token-aware
    /// shortcuts for common controls. Everything built here uses
    /// <see cref="UiTokens"/> and inherits the shared
    /// <see cref="CthangoverTheme"/> once it is applied to a root.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Rounded frame from tokens; pass transparent border to omit it.</summary>
        public static StyleBoxFlat Frame(int radius, Color background, Color? border = null, int borderWidth = 1)
        {
            var style = new StyleBoxFlat
            {
                BgColor = background,
                CornerRadiusTopLeft = radius,
                CornerRadiusTopRight = radius,
                CornerRadiusBottomLeft = radius,
                CornerRadiusBottomRight = radius,
            };

            if (border.HasValue)
            {
                style.BorderColor = border.Value;
                style.BorderWidthLeft = borderWidth;
                style.BorderWidthRight = borderWidth;
                style.BorderWidthTop = borderWidth;
                style.BorderWidthBottom = borderWidth;
            }

            return style;
        }

        public static Label Label(string text, int fontSize = UiTokens.FontBodyLarge, Color? color = null)
        {
            var label = new Label
            {
                Text = text ?? string.Empty,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            if (color.HasValue)
                label.AddThemeColorOverride("font_color", color.Value);
            return label;
        }

        public static Label SectionTitle(string text, int fontSize = UiTokens.FontBodyLarge)
        {
            var label = Label(text.ToUpperInvariant(), fontSize, UiTokens.Muted);
            label.AddThemeConstantOverride("outline_size", 0);
            return label;
        }

        public static Label EmptyState(string text, int fontSize = UiTokens.FontBodyLarge)
        {
            var label = Label(text, fontSize, UiTokens.Faint);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            return label;
        }

        public static Label Hint(string text, int fontSize = UiTokens.FontBodySmall)
            => Label(text, fontSize, UiTokens.Faint);

        /// <summary>Small bordered caption chip: fill 9%, border 50%, text in the accent colour.</summary>
        public static Control Badge(string text, Color accent, int fontSize = UiTokens.FontTiny)
        {
            var style = Frame(UiTokens.RadiusXs, UiTokens.BadgeFill(accent), UiTokens.BadgeLine(accent));
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 2;

            var badge = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            badge.AddThemeStyleboxOverride("panel", style);

            var label = Label(text, fontSize, accent);
            label.Uppercase = true;
            badge.AddChild(label);
            return badge;
        }

        /// <summary>Property chip ("Attack 1.0") with a muted key and gold value.</summary>
        public static Control PropertyChip(string key, string value)
        {
            var style = Frame(UiTokens.RadiusXs, UiTokens.AccentFaint, UiTokens.AccentLineWeak);
            style.ContentMarginLeft = 6;
            style.ContentMarginRight = 6;
            style.ContentMarginTop = 1;
            style.ContentMarginBottom = 1;

            var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            chip.AddThemeStyleboxOverride("panel", style);

            var row = Row(UiTokens.Space1);
            row.AddChild(Label(key, UiTokens.FontTiny, UiTokens.ChipKey));
            row.AddChild(Label(value, UiTokens.FontTiny, UiTokens.ChipValue));
            chip.AddChild(row);
            return chip;
        }

        /// <summary>Character stat chip ("HP 100/100") on its own inset surface.</summary>
        public static Control StatChip(string key, string value, Color? valueColor = null)
        {
            var style = Frame(UiTokens.RadiusXs, UiTokens.StatBg, UiTokens.StatLine);
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 2;

            var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            chip.AddThemeStyleboxOverride("panel", style);

            var row = Row(UiTokens.Space1);
            var keyLabel = Label(key.ToUpperInvariant(), UiTokens.FontTiny, UiTokens.Muted);
            keyLabel.AddThemeConstantOverride("outline_size", 0);
            row.AddChild(keyLabel);
            row.AddChild(Label(value, UiTokens.FontSmall, valueColor ?? UiTokens.Ink));
            chip.AddChild(row);
            return chip;
        }

        public static Button Button(string text, ButtonKind kind = ButtonKind.Secondary)
        {
            var button = new Button { Text = text ?? string.Empty };
            switch (kind)
            {
                case ButtonKind.Primary:
                    CthangoverTheme.ApplyPrimary(button);
                    break;
                case ButtonKind.Danger:
                    CthangoverTheme.ApplyDanger(button);
                    break;
                case ButtonKind.Icon:
                    CthangoverTheme.ApplyIcon(button);
                    break;
                case ButtonKind.Tab:
                    CthangoverTheme.ApplyTab(button);
                    break;
            }
            return button;
        }

        public static Button IconButton(string glyph, string tooltip = null)
        {
            var button = Button(glyph, ButtonKind.Icon);
            if (!string.IsNullOrEmpty(tooltip))
                button.TooltipText = tooltip;
            return button;
        }

        public static HBoxContainer Row(int separation = UiTokens.Space4)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", separation);
            return row;
        }

        public static VBoxContainer Column(int separation = UiTokens.Space4)
        {
            var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            column.AddThemeConstantOverride("separation", separation);
            return column;
        }

        /// <summary>Invisible expanding filler between controls.</summary>
        public static Control Spacer()
        {
            return new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
        }

        public static TextureRect Icon(Texture2D texture, int size)
        {
            return new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = new Vector2(size, size),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        }

        /// <summary>
        /// Dresses the full-screen backdrop of a gameplay panel: the panel keeps
        /// its own <see cref="ColorRect"/> but it becomes an opaque dark surface,
        /// and the shared <c>panels_background</c> texture is stretched over the
        /// whole rect. Panels sit above live scenes, and the previous 0.9-alpha
        /// backdrop let scene buttons bleed through, which made the panel close
        /// buttons look noisy; an opaque backing removes them. The colour stays
        /// underneath as a fallback because the texture ships in the optional
        /// "interface" mod and may not resolve, in which case the panel still
        /// gets a solid surface.
        /// </summary>
        public static void ApplyPanelBackdrop(ColorRect backdrop)
        {
            if (backdrop == null)
                return;

            backdrop.Color = UiTokens.Page;

            var texture = new TextureRect
            {
                Texture = Textures.Resolve("panels_background"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            texture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            backdrop.AddChild(texture);
        }
    }
}
