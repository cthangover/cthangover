using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Determines whether the running main scene is the pre-game launcher.
    /// When the game starts directly into <c>scenes/menu/launcher.tscn</c>,
    /// the launcher renders its boot overlay first and drives the whole boot
    /// pipeline itself through the Boot orchestrator (pure .NET work on a
    /// background thread, Godot-bound work on the main thread). Every other
    /// entry point — integration tests, direct scene launches for debugging —
    /// keeps the synchronous autoload path.
    ///
    /// This class is only about boot-mode detection: mod readiness now lives
    /// in <see cref="ModRegistry.IsInitialized"/> and the Boot phases, and the
    /// wait-handle mechanics were removed together with lazy initialization.
    /// The flag is read from arbitrary threads and therefore stays volatile.
    /// </summary>
    public static class BootController
    {
        private static readonly object detectLock = new();

        private static volatile bool _detected;
        private static volatile bool _isLauncherBoot;

        /// <summary>True when the current main scene is the pre-game launcher.</summary>
        public static bool IsLauncherBoot
        {
            get
            {
                Detect();
                return _isLauncherBoot;
            }
        }

        /// <summary>
        /// Idempotently resolves the effective main scene path and flags a
        /// launcher boot. A positional <c>*.tscn</c> command-line argument (used
        /// by direct scene launches and tests) overrides the project
        /// <c>application/run/main_scene</c> setting.
        /// </summary>
        public static void Detect()
        {
            if (_detected)
                return;

            lock (detectLock)
            {
                if (_detected)
                    return;
                _detected = true;

                if (!GameEnvironment.IsRuntime)
                    return;

                var args = OS.GetCmdlineArgs();
                string scene = null;
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg.StartsWith("--"))
                        continue;
                    if (arg.EndsWith(".tscn") || arg.EndsWith(".scn") || arg.EndsWith(".escn"))
                        scene = arg;
                }

                if (scene == null)
                    scene = ProjectSettings.GetSetting("application/run/main_scene").AsString();

                _isLauncherBoot = scene != null &&
                    scene.Replace('\\', '/').EndsWith("scenes/menu/launcher.tscn", System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
