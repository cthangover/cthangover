using System;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Core-side bridge for the scene lighting stack. The implementation
    /// (locating the scene's lighting controller and pushing maps to it) lives
    /// in the time_base engine mod; the kernel — SceneManager's scene-switch
    /// reset and background updates — only knows these delegate slots. The mod
    /// installs its hooks through <see cref="Register"/> when the mod pipeline
    /// reaches <c>IModInitializer.OnModResourcesReady</c>. Registration is
    /// first-wins: reloading the mod re-runs its static initializers, and a
    /// second hook pair must never stack on top of the first. While no hooks
    /// are bound (kernel built without the time mod, or calls before mods
    /// load) invocations are skipped with a warning instead of throwing, so a
    /// scene switch cannot crash the runtime.
    /// </summary>
    public static class LightingBridge
    {
        private static readonly object Gate = new();
        private static Action _clearSceneLights;
        private static Action<Texture2D, Texture2D> _setupBackgroundMaps;

        /// <summary>Installs the lighting hooks; first registration wins, re-registrations are logged and ignored.</summary>
        public static void Register(Action clearSceneLights, Action<Texture2D, Texture2D> setupBackgroundMaps)
        {
            if (clearSceneLights == null || setupBackgroundMaps == null)
                return;

            lock (Gate)
            {
                if (_clearSceneLights != null)
                {
                    GameLogger.Log("LIGHT", "LightingBridge: lighting hooks already registered; ignoring re-registration", LogLevel.Warning);
                    return;
                }

                _clearSceneLights = clearSceneLights;
                _setupBackgroundMaps = setupBackgroundMaps;
            }
        }

        /// <summary>
        /// Resets static lights and both lighting maps for the outgoing scene.
        /// Skipped with a warning when no time_base implementation is bound.
        /// </summary>
        public static void ClearSceneLights()
        {
            Action hook;
            lock (Gate)
                hook = _clearSceneLights;

            if (hook == null)
            {
                GameLogger.Log("LIGHT", "LightingBridge: no lighting hooks bound (time_base not loaded); scene light reset skipped", LogLevel.Warning);
                return;
            }

            hook();
        }

        /// <summary>
        /// Pushes background depth/albedo textures onto the lighting
        /// controller. Skipped with a warning when no time_base
        /// implementation is bound.
        /// </summary>
        public static void SetupBackgroundMaps(Texture2D depth, Texture2D albedo)
        {
            Action<Texture2D, Texture2D> hook;
            lock (Gate)
                hook = _setupBackgroundMaps;

            if (hook == null)
            {
                GameLogger.Log("LIGHT", "LightingBridge: no lighting hooks bound (time_base not loaded); background maps update skipped", LogLevel.Warning);
                return;
            }

            hook(depth, albedo);
        }
    }
}
