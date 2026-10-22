using Godot;

namespace Cthangover.Core.UI.Theming
{
    /// <summary>
    /// The switch of every <see cref="CheckButton"/>: a rounded track with a knob, drawn in the
    /// theme's tokens instead of Godot's default icons. Off is a dark track with a grey knob on
    /// the left, on is a gold track with the knob on the right. The button itself has no
    /// background in any state (it used to inherit the plain button's panel, a light box around
    /// an unchecked switch); hover and focus light up the track instead — Godot has no hover
    /// icons for a CheckButton, so every switch that enters the tree swaps in the "lit" textures
    /// while it is under the mouse or focused (icon overrides, removed again on leave).
    /// Pixels are drawn with a signed distance (anti-aliased edges), once, when the theme is built.
    /// </summary>
    public static class ToggleSwitchTextures
    {
        private const int Width = 38;
        private const int Height = 22;
        private const float Knob = 7f;

        /// <summary>Unchecked, resting.</summary>
        public static readonly Texture2D Off = Draw(false, UiTokens.Surface3, UiTokens.LineStrong, UiTokens.Muted);
        /// <summary>Unchecked, under the mouse or focused.</summary>
        public static readonly Texture2D OffLit = Draw(false, UiTokens.SurfaceHover, UiTokens.Alpha(UiTokens.Accent, 0.6f), UiTokens.Body);
        /// <summary>Checked, resting.</summary>
        public static readonly Texture2D On = Draw(true, UiTokens.Alpha(UiTokens.Accent, 0.28f), UiTokens.Accent, UiTokens.AccentBright);
        /// <summary>Checked, under the mouse or focused.</summary>
        public static readonly Texture2D OnLit = Draw(true, UiTokens.Alpha(UiTokens.Accent, 0.4f), UiTokens.AccentBright, UiTokens.InkBright);
        /// <summary>Unchecked and disabled.</summary>
        public static readonly Texture2D OffDisabled = Draw(false, UiTokens.Surface3, UiTokens.LineDisabled, UiTokens.Faint);
        /// <summary>Checked and disabled.</summary>
        public static readonly Texture2D OnDisabled = Draw(true, UiTokens.Alpha(UiTokens.Accent, 0.1f), UiTokens.Alpha(UiTokens.Accent, 0.3f), UiTokens.Alpha(UiTokens.AccentBright, 0.45f));

        /// <summary>Puts the switch into <paramref name="theme"/>: track icons and empty backgrounds of <c>CheckButton</c>.</summary>
        public static void Apply(Theme theme)
        {
            const string type = "CheckButton";
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            {
                var empty = new StyleBoxEmpty();
                empty.ContentMarginLeft = 2;
                empty.ContentMarginRight = 2;
                empty.ContentMarginTop = 2;
                empty.ContentMarginBottom = 2;
                theme.SetStylebox(state, type, empty);
            }

            theme.SetIcon("unchecked", type, Off);
            theme.SetIcon("checked", type, On);
            theme.SetIcon("unchecked_mirrored", type, Off);
            theme.SetIcon("checked_mirrored", type, On);
            theme.SetIcon("unchecked_disabled", type, OffDisabled);
            theme.SetIcon("checked_disabled", type, OnDisabled);
            theme.SetIcon("unchecked_disabled_mirrored", type, OffDisabled);
            theme.SetIcon("checked_disabled_mirrored", type, OnDisabled);

            if (!tracking && Engine.GetMainLoop() is SceneTree tree)
            {
                tracking = true;
                tree.NodeAdded += node =>
                {
                    if (node is CheckButton button)
                        Track(button);
                };
            }
        }

        private static bool tracking;

        private static void Track(CheckButton button)
        {
            void Refresh()
            {
                if (!GodotObject.IsInstanceValid(button))
                    return;
                // Only switches drawn by this theme: a button with icons of its own keeps them.
                button.RemoveThemeIconOverride("unchecked");
                button.RemoveThemeIconOverride("checked");
                if (button.GetThemeIcon("unchecked") != Off)
                    return;
                var lit = !button.Disabled && (button.IsHovered() || button.HasFocus());
                if (!lit)
                    return;
                button.AddThemeIconOverride("unchecked", OffLit);
                button.AddThemeIconOverride("checked", OnLit);
            }

            button.MouseEntered += () => Callable.From(Refresh).CallDeferred();
            button.MouseExited += () => Callable.From(Refresh).CallDeferred();
            button.FocusEntered += Refresh;
            button.FocusExited += Refresh;
        }

        private static Texture2D Draw(bool on, Color fill, Color border, Color knob)
        {
            var image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
            var radius = (Height - 1) / 2f;
            var knobCenter = new Vector2(on ? Width - 1 - radius : radius, radius);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var p = new Vector2(x, y);
                    // Distance to the track: a capsule from the left to the right half-circle centre.
                    var cx = Mathf.Clamp(p.X, radius, Width - 1 - radius);
                    var track = p.DistanceTo(new Vector2(cx, radius)) - radius;

                    var color = new Color(0f, 0f, 0f, 0f);
                    color = Over(color, fill, Coverage(track + 0.5f));
                    color = Over(color, border, Coverage(track + 0.5f) - Coverage(track + 1.5f));
                    color = Over(color, knob, Coverage(p.DistanceTo(knobCenter) - Knob + 0.5f));
                    image.SetPixel(x, y, color);
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>Pixel coverage of a shape from its signed distance (negative inside).</summary>
        private static float Coverage(float distance) => Mathf.Clamp(0.5f - distance, 0f, 1f);

        private static Color Over(Color below, Color top, float coverage)
        {
            var a = Mathf.Clamp(top.A * coverage, 0f, 1f);
            if (a <= 0f)
                return below;
            var outA = a + below.A * (1f - a);
            var rgb = (new Vector3(top.R, top.G, top.B) * a + new Vector3(below.R, below.G, below.B) * below.A * (1f - a)) / outA;
            return new Color(rgb.X, rgb.Y, rgb.Z, outA);
        }
    }
}
