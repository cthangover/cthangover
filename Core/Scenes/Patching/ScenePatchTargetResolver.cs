using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Translates the target path a patch declares in its <c>[ext_resource type="PackedScene"]</c>
    /// header into the canonical scene key used by <see cref="TscnSceneResolver"/>.
    /// <para/>
    /// Three spellings denote the same scene: the resolver key (<c>scenes/menu/main_menu.tscn</c>),
    /// the native form (<c>res://scenes/menu/main_menu.tscn</c>) and the on-disk form of a mod
    /// scene (<c>res://mods/&lt;mod&gt;/scenes/menu/main_menu.tscn</c>, which is what the Godot editor
    /// writes when the mod lives inside the project). Normalisation strips the scheme and the
    /// <c>mods/&lt;mod&gt;/</c> prefix; the resolver then matches by suffix against the collected
    /// scene index so a patch works regardless of which mod won the override.
    /// </summary>
    public static class ScenePatchTargetResolver
    {
        /// <summary>
        /// Converts a declared target path into canonical mod-relative form: forward slashes,
        /// no <c>res://</c> scheme, no leading slash, and no <c>mods/&lt;mod&gt;/</c> prefix.
        /// Returns an empty string for null/empty input.
        /// </summary>
        public static string Normalize(string declaredPath)
        {
            if (string.IsNullOrEmpty(declaredPath))
                return "";

            var path = declaredPath.Replace('\\', '/');
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("res://".Length);
            path = path.TrimStart('/');

            var segments = path.Split('/');
            if (segments.Length >= 3 && segments[0].Equals("mods", StringComparison.OrdinalIgnoreCase))
                path = string.Join("/", segments, 2, segments.Length - 2);

            return ModPath.Normalize(path);
        }

        /// <summary>
        /// True when a patch targeting <paramref name="declaredPath"/> applies to the scene
        /// registered under <paramref name="sceneKey"/>. Exact match, or the declared path
        /// ends with the scene key (covers extra prefixes the normaliser did not strip).
        /// </summary>
        public static bool MatchesSceneKey(string sceneKey, string declaredPath)
        {
            if (string.IsNullOrEmpty(sceneKey) || string.IsNullOrEmpty(declaredPath))
                return false;

            var normalized = Normalize(declaredPath);
            if (normalized.Length == 0)
                return false;

            if (string.Equals(normalized, sceneKey, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalized.EndsWith("/" + sceneKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Picks the winning scene key for a declared target from the collected scene index.
        /// Exact matches win over suffix matches, mirroring how <c>TscnSceneResolver</c> resolves
        /// override conflicts (the index itself already holds one entry per key).
        /// </summary>
        public static string Resolve(string declaredPath, IEnumerable<string> availableKeys)
        {
            var normalized = Normalize(declaredPath);
            if (normalized.Length == 0 || availableKeys == null)
                return null;

            string suffixMatch = null;
            foreach (var key in availableKeys)
            {
                if (key == null)
                    continue;

                if (string.Equals(key, normalized, StringComparison.OrdinalIgnoreCase))
                    return key;

                if (suffixMatch == null && normalized.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase))
                    suffixMatch = key;
            }

            return suffixMatch;
        }
    }
}
