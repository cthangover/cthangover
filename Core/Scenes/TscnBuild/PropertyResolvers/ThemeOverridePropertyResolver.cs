using System;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.PropertyResolvers
{
    /// <summary>
    /// Applies editor-written theme overrides (<c>theme_override_constants/separation</c>,
    /// <c>theme_override_font_sizes/font_size</c>, <c>theme_override_colors/...</c>, and the
    /// stylebox/font/icon variants) through Control's <c>AddTheme*Override</c> API.
    /// <para/>
    /// The runtime parser used to declare these nested keys unsupported, which is invisible for
    /// mod scenes authored with scripts but not for native scenes: <c>scenes/menu/main_menu.tscn</c>
    /// and <c>settings_menu.tscn</c> rely on them, and patched native scenes must build through
    /// the runtime pipeline, so dropping them would visibly change the menu. Handling them here
    /// closes that gap without changing how mod scenes are authored.
    /// <para/>
    /// Values that are <see cref="TscnPropertyValue"/> references (an override pointing at an
    /// ext/sub resource) are deliberately left to the existing resource resolvers.
    /// </summary>
    public class ThemeOverridePropertyResolver : IPropertyResolver
    {
        private const string Prefix = "theme_override_";

        /// <summary>Claims plain-valued <c>theme_override_*/</c> keys; reference-valued overrides are left to the resource resolvers.</summary>
        public bool CanResolve(string key, object value)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith(Prefix, StringComparison.Ordinal)
                && key.IndexOf('/') > 0
                && value is not TscnPropertyValue;
        }

        /// <summary>Routes the override to the matching AddTheme*Override by category (constants, font_sizes, colors, fonts, icons, styles).</summary>
        public void Apply(Node node, string key, object value, PropertyContext ctx)
        {
            if (node is not Control control)
            {
                GameLogger.Log("TSCN_BUILD", $"ThemeOverridePropertyResolver: '{key}' on non-Control '{node.Name}' ignored", LogLevel.Warning);
                return;
            }

            var slash = key.IndexOf('/');
            var category = key.Substring(Prefix.Length, slash - Prefix.Length);
            var name = key.Substring(slash + 1);
            var variant = SubResourceFactory.ToVariant(value);

            try
            {
                switch (category)
                {
                    case "constants":
                        control.AddThemeConstantOverride(name, variant.AsInt32());
                        break;
                    case "font_sizes":
                        control.AddThemeFontSizeOverride(name, variant.AsInt32());
                        break;
                    case "colors":
                        control.AddThemeColorOverride(name, variant.AsColor());
                        break;
                    case "fonts":
                        control.AddThemeFontOverride(name, variant.As<Font>());
                        break;
                    case "icons":
                        control.AddThemeIconOverride(name, variant.As<Texture2D>());
                        break;
                    case "styles":
                        control.AddThemeStyleboxOverride(name, variant.As<StyleBox>());
                        break;
                    default:
                        GameLogger.Log("TSCN_BUILD", $"ThemeOverridePropertyResolver: unknown category '{category}' for '{key}'", LogLevel.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("TSCN_BUILD", $"ThemeOverridePropertyResolver: failed to apply '{key}': {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
