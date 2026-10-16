using System;
using System.Linq;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Parses the first-class <c>--scene=&lt;logical&gt;</c> launch flag from
    /// Godot user arguments (everything after <c>--</c> on the command line).
    /// The flag is a core navigation feature, not a test hook: when present,
    /// the launcher boots the mod pipeline as usual and then jumps straight
    /// into the requested mod-defined scene instead of showing the launcher UI
    /// or the main menu. Only logical scene names (resolved through
    /// <see cref="SceneManager"/>) are accepted — a raw <c>.tscn</c> path can
    /// already be passed to Godot natively as the boot scene.
    /// Kept separate from <see cref="SceneManager"/> so argument parsing stays
    /// independent of the autoload lifecycle and is trivially reusable by any
    /// future entry point.
    /// </summary>
    public static class SceneLaunchArgs
    {
        /// <summary>
        /// Returns the logical scene name requested via <c>--scene=&lt;id&gt;</c>,
        /// or null when the flag is absent or empty. Only the first occurrence
        /// wins; repeated flags are ignored. Guarded by
        /// <see cref="GameEnvironment.IsRuntime"/> because
        /// <c>OS.GetCmdlineUserArgs()</c> is a Godot API that must not be
        /// touched from pure test environments.
        /// </summary>
        public static string RequestedSceneName()
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            return OS.GetCmdlineUserArgs()
                .Where(a => a.StartsWith("--scene=", StringComparison.Ordinal))
                .Select(a => a.Substring("--scene=".Length).Trim())
                .FirstOrDefault(id => !string.IsNullOrEmpty(id));
        }
    }
}
