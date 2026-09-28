using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Self-registration point for mod-provided scene routes. The core keeps no
    /// knowledge of gameplay-specific scene names or paths: a mod's initializer
    /// calls <see cref="Register"/> with a logical name (the value scenarios use
    /// in <c>switch_scene</c>) and a mod-relative tscn path. <see cref="SceneManager"/>
    /// consults the registry before falling back to scenario composition, and
    /// <see cref="GodotSceneService"/> asks it for the transition style and music
    /// bucket when the path is loaded. A later registration with the same logical
    /// name overrides the earlier one (mod load order). Cleared on mod reload
    /// together with the other UI registries.
    /// </summary>
    public static class SceneRouteRegistry
    {
        private static readonly Dictionary<string, SceneRoute> routes =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers (or replaces) a route. The logical name is matched
        /// case-insensitively, so <c>switch_scene Battle</c> and
        /// <c>scene=battle</c> resolve to the same entry.
        /// </summary>
        public static void Register(string logicalName, string modRelativeScenePath, bool fade,
            string musicScene = null, Audio.MusicType music = Audio.MusicType.Ambient)
        {
            if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(modRelativeScenePath))
            {
                GameLogger.Log("SCENE", "SceneRouteRegistry.Register: empty name or path ignored", LogLevel.Error);
                return;
            }

            routes[logicalName] = new SceneRoute
            {
                LogicalName = logicalName,
                ScenePath = modRelativeScenePath,
                Fade = fade,
                MusicScene = musicScene,
                Music = music,
            };

            GameLogger.Log("SCENE", $"SceneRouteRegistry: '{logicalName}' -> '{modRelativeScenePath}' (fade={fade})");
        }

        /// <summary>Looks up a route by logical name; false when no mod registered it.</summary>
        public static bool TryGet(string logicalName, out SceneRoute route)
        {
            route = null;
            return !string.IsNullOrEmpty(logicalName) && routes.TryGetValue(logicalName, out route);
        }

        /// <summary>
        /// Finds the route whose scene path matches the pending scene path, so
        /// the transition and music layers do not need to know logical names.
        /// </summary>
        public static SceneRoute FindByPath(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return null;

            foreach (var route in routes.Values)
            {
                if (string.Equals(route.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                    return route;
            }

            return null;
        }

        /// <summary>Drops all registrations; next route lookup fails until a mod re-registers.</summary>
        public static void Clear()
        {
            routes.Clear();
        }
    }
}
