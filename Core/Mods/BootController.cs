using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Determines whether the running main scene is the pre-game launcher and
    /// tracks whether mod initialisation has completed. When the game starts
    /// directly into <c>scenes/menu/launcher.tscn</c>, autoloads defer the heavy
    /// mod load so the launcher can render its boot overlay first and drive the
    /// load itself (pure .NET work on a background thread, Godot-bound work on
    /// the main thread). Every other entry point — integration tests, direct
    /// scene launches for debugging — keeps the synchronous autoload path.
    /// </summary>
    public static class BootController
    {
        private static bool _detected;
        private static bool _isLauncherBoot;
        private static bool _modsInitialized;

        /// <summary>True when the current main scene is the pre-game launcher.</summary>
        public static bool IsLauncherBoot
        {
            get
            {
                Detect();
                return _isLauncherBoot;
            }
        }

        /// <summary>True once mod discovery, compilation and Godot-side init have finished.</summary>
        public static bool ModsInitialized => _modsInitialized;

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

        /// <summary>Marks mod initialisation as complete.</summary>
        public static void MarkModsInitialized() => _modsInitialized = true;
    }
}
