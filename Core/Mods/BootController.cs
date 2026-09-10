using System.Threading;
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
    ///
    /// The flags are read from arbitrary threads: <see cref="ModRegistry"/>'s
    /// lazy entry points may be called by factories or tests while the launcher
    /// is still compiling on a background thread. They are therefore volatile,
    /// and <see cref="MarkModsInitialized"/> additionally releases a wait
    /// handle so background callers can block until the launcher finishes
    /// instead of observing a half-built registry.
    /// </summary>
    public static class BootController
    {
        private static readonly object detectLock = new();
        private static readonly ManualResetEventSlim modsReady = new(false);

        private static volatile bool _detected;
        private static volatile bool _isLauncherBoot;
        private static volatile bool _modsInitialized;

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

        /// <summary>Marks mod initialisation as complete and wakes waiting threads.</summary>
        public static void MarkModsInitialized()
        {
            _modsInitialized = true;
            modsReady.Set();
        }

        /// <summary>
        /// Blocks until <see cref="MarkModsInitialized"/> has been called, or
        /// until <paramref name="timeoutMs"/> elapses. Used by background
        /// callers that reach <see cref="ModRegistry.Initialize"/> while the
        /// launcher owns the pipeline — the main thread must never call this,
        /// because it is the thread that eventually performs the finalize.
        /// </summary>
        public static bool WaitForModsInitialized(int timeoutMs)
        {
            return modsReady.Wait(timeoutMs);
        }
    }
}
