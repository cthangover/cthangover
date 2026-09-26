using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Compatibility facade over the central <see cref="CthangoverTheme"/> and
    /// <see cref="UiTokens"/>. The mod manager once carried its own Nexus-like
    /// palette; now every colour is a design token and the shared theme cascades
    /// from the screen root. Public names are preserved so existing widgets keep
    /// compiling, but each one is an alias or a thin wrapper around the core
    /// theming API. New code should reference <see cref="UiTokens"/>,
    /// <see cref="CthangoverTheme"/> and <see cref="UiKit"/> directly.
    /// </summary>
    public static class ModManagerTheme
    {
        /// <summary>Page background.</summary>
        public static readonly Color BgBase = UiTokens.Surface0;

        /// <summary>Card and sidebar surface.</summary>
        public static readonly Color SurfaceLow = UiTokens.Surface1;

        /// <summary>Raised control surface.</summary>
        public static readonly Color SurfaceRaised = UiTokens.Surface3;

        /// <summary>Primary text.</summary>
        public static readonly Color TextStrong = UiTokens.InkBright;

        /// <summary>Secondary text.</summary>
        public static readonly Color TextMuted = UiTokens.Muted;

        /// <summary>Hairline border.</summary>
        public static readonly Color BorderWeak = UiTokens.Line;

        /// <summary>Control border.</summary>
        public static readonly Color BorderStrong = UiTokens.LineStrong;

        /// <summary>Gold accent.</summary>
        public static readonly Color Accent = UiTokens.Accent;

        /// <summary>Text on accent buttons.</summary>
        public static readonly Color AccentText = UiTokens.Surface0;

        /// <summary>Danger red for destructive actions.</summary>
        public static readonly Color Danger = UiTokens.Danger;

        /// <summary>Returns the single shared theme; the manager no longer owns a private one.</summary>
        public static Theme Create() => CthangoverTheme.Get();

        /// <summary>
        /// Style for a mod tile. The negative top/left content margins shift the
        /// cover image 1px up and make it 1px wider so it sits flush over the
        /// card border. A conflicting catalog variant gets a danger border.
        /// </summary>
        public static StyleBoxFlat CreateCardStyle(bool conflict)
        {
            var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface1, conflict ? UiTokens.Danger : UiTokens.Line);
            style.ContentMarginTop = -1;
            style.ContentMarginLeft = -1;
            return style;
        }

        /// <summary>Applies the shared primary accent variation to a button.</summary>
        public static void ApplyPrimaryButton(Button button) => CthangoverTheme.ApplyPrimary(button);

        /// <summary>Applies the shared danger variation to a button.</summary>
        public static void ApplyDangerButton(Button button) => CthangoverTheme.ApplyDanger(button);

        /// <summary>Creates a flat style box with an optional border and padding.</summary>
        public static StyleBoxFlat MakeBox(Color background, Color border, int radius, int paddingH, int paddingV)
        {
            var style = UiKit.Frame(radius, background, border);
            style.ContentMarginLeft = paddingH;
            style.ContentMarginRight = paddingH;
            style.ContentMarginTop = paddingV;
            style.ContentMarginBottom = paddingV;
            return style;
        }
    }
}
