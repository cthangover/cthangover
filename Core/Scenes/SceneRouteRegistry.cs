using System;
using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
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
    /// <para/>
    /// Routes are registered by mod initializers
    /// (<c>IModInitializer.OnModResourcesReady</c>), not collected from manifests,
    /// so <see cref="Rebuild"/> only resets the table and lets the initializers
    /// re-register. The registry instance lives in <see cref="AppServices.Current"/>;
    /// the static members below are a facade delegating to it, so existing callers
    /// keep working unchanged.
    /// </summary>
    public class SceneRouteRegistry : IGameRegistry
    {
        private readonly Dictionary<string, SceneRoute> routes =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> shells = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static SceneRouteRegistry Owned => AppServices.Current.SceneRoutes;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract.
        /// </summary>
        internal SceneRouteRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "scene_routes";

        /// <summary>
        /// Routes are re-populated by IModInitializer.OnModResourcesReady, not
        /// collected from manifests — rebuilding just resets the table so the
        /// initializers re-register from scratch.
        /// </summary>
        public void Rebuild()
        {
            // Data-only rebuilds (including test recovery) do not unload shell owners.
            // Mod-set replacement calls Clear first and initializers register fresh paths.
            routes.Clear();
        }

        /// <summary>
        /// Drops all registrations; next route lookup fails until a mod
        /// re-registers. Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        void IGameRegistry.Clear()
        {
            Reset();
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>
        /// Registers (or replaces) a route. The logical name is matched
        /// case-insensitively, so <c>switch_scene Battle</c> and
        /// <c>scene=battle</c> resolve to the same entry.
        /// </summary>
        public static void Register(string logicalName, string modRelativeScenePath, bool fade,
            string musicScene = null, Audio.MusicType music = Audio.MusicType.Ambient)
            => Owned.RegisterRoute(logicalName, modRelativeScenePath, fade, musicScene, music);

        /// <summary>Looks up a route by logical name; false when no mod registered it.</summary>
        public static bool TryGet(string logicalName, out SceneRoute route)
            => Owned.TryGetRoute(logicalName, out route);

        /// <summary>
        /// Finds the route whose scene path matches the pending scene path, so
        /// the transition and music layers do not need to know logical names.
        /// </summary>
        public static SceneRoute FindByPath(string scenePath)
            => Owned.FindByScenePath(scenePath);

        /// <summary>Drops all registrations; next route lookup fails until a mod re-registers.</summary>
        public static void Clear()
            => Owned.Reset();

        // ---- Instance implementation --------------------------------------

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void Reset()
        {
            routes.Clear();
            shells.Clear();
        }

        /// <summary>Registers a logical scene shell without coupling the player to its dimensionality or mechanics.</summary>
        public void RegisterShell(string kind, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(scenePath)) throw new ArgumentException("Shell kind/path required");
            shells[kind] = scenePath;
        }

        /// <summary>Removes a shell when its owning mod unloads, including unloads without a full registry clear.</summary>
        public void UnregisterShell(string kind) => shells.Remove(kind);

        /// <summary>2D retains its established path; additional kinds must be registered by their owning mod.</summary>
        public string GetShellPath(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.Equals(kind, "2d", StringComparison.OrdinalIgnoreCase))
                return GodotSceneService.GetScenePath(GodotSceneType.BaseScene);
            return shells.TryGetValue(kind, out var path) ? path : throw new KeyNotFoundException("Unknown shell: " + kind);
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void RegisterRoute(string logicalName, string modRelativeScenePath, bool fade,
            string musicScene, Audio.MusicType music)
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

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public bool TryGetRoute(string logicalName, out SceneRoute route)
        {
            route = null;
            return !string.IsNullOrEmpty(logicalName) && routes.TryGetValue(logicalName, out route);
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public SceneRoute FindByScenePath(string scenePath)
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
    }
}
