using System;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Bootstrap
{
    /// <summary>
    /// Phases of the unified boot pipeline, in execution order. Comparison by
    /// value is meaningful: a later phase implies that all earlier ones finished.
    /// </summary>
    public enum BootPhase
    {
        NotStarted = 0,
        /// <summary>Godot-bound paths resolved on the main thread (mod roots, caches, config).</summary>
        PreparingPaths,
        /// <summary>Discovery, manifest reading and Roslyn compilation of mod assemblies.</summary>
        Discovering,
        /// <summary>Compiled assemblies registered, GDScript loaded, mod load order fixed.</summary>
        FinalizingMods,
        /// <summary>Mod set is final: factories and resolvers may read mod data.</summary>
        ModsReady,
        /// <summary>Locale merged from all loaded mods and activated.</summary>
        Locale,
        /// <summary>Scene/UI registries collected, mod initializers notified, mod settings loaded.</summary>
        Scenes,
        /// <summary>Everything is usable.</summary>
        Ready,
    }

    /// <summary>How the boot pipeline is being executed.</summary>
    public enum BootMode
    {
        None,
        /// <summary>Launcher-driven: heavy phases on a background thread, boot overlay visible.</summary>
        LauncherAsync,
        /// <summary>Blocking synchronous run before the main scene (tests, direct scene launches).</summary>
        HeadlessSync,
    }

    /// <summary>
    /// Single orchestrator of game initialization. Owns the exact order of boot
    /// phases. Both entry points drive the SAME sequence through this class:
    /// <list type="bullet">
    /// <item><see cref="RunLauncherAsync"/> — the pre-game launcher renders a boot
    /// overlay while discovery and Roslyn compilation run on a background thread;</item>
    /// <item><see cref="RunSync"/> — direct scene launches, SceneSwitcher and
    /// integration tests, executed from the SceneManager autoload _Ready so the
    /// whole pipeline completes before any main-scene node runs.</item>
    /// </list>
    ///
    /// Thread contract (mirrors the old launcher-driven flow): path preparation,
    /// finalize, runtime, locale and scene registries run on the Godot main
    /// thread; only discovery/compilation is pure .NET and moves to a background
    /// thread in the launcher mode.
    ///
    /// Until <see cref="ModsReady"/>, no subsystem may pull data through
    /// <see cref="ModRegistry"/> or the factories: lazy self-initialization was
    /// removed deliberately, and <see cref="ModRegistry"/> logs a loud error
    /// instead of silently booting on first touch. From <see cref="ModsReady"/>
    /// on the mod set is final and the pipeline itself consumes it (runtime,
    /// starter inventory). If a new caller triggers that refusal error, the fix
    /// is to consume the data after readiness (or behind a readiness guard like
    /// the audio mod's music player does) — never to re-add a lazy Initialize.
    /// </summary>
    public static class Boot
    {
        private static readonly object startLock = new();

        /// <summary>Current pipeline phase; <c>NotStarted</c> before the first run.</summary>
        public static BootPhase Phase { get; private set; } = BootPhase.NotStarted;

        /// <summary>Mode of the current (or last) run; <c>None</c> before the first run.</summary>
        public static BootMode Mode { get; private set; } = BootMode.None;

        /// <summary>True once the full pipeline finished; registries and services are usable.</summary>
        public static bool IsReady => Phase == BootPhase.Ready;

        /// <summary>True once the mod set is final: factories may read mod data.</summary>
        public static bool ModsReady => Phase >= BootPhase.ModsReady;

        /// <summary>True while the pipeline is executing in any mode.</summary>
        public static bool IsBooting => Mode != BootMode.None && !IsReady;

        /// <summary>
        /// Synchronous boot for everything that is not the pre-game launcher:
        /// SceneSwitcher, integration test scenes, direct scene runs and editor
        /// "play current scene". Called from the SceneManager autoload _Ready,
        /// which is guaranteed to run after GameData exists and before the main
        /// scene's nodes.
        /// </summary>
        public static void RunSync(SceneManager sceneManager)
        {
            if (!Begin(BootMode.HeadlessSync))
                return;

            try
            {
                Phase = BootPhase.PreparingPaths;
                // The whole pipeline runs under ModRegistry's pipeline lock
                // (pipelineRunning is set for its duration), so a stray
                // concurrent Initialize from a background thread or tooling is
                // serialized instead of racing these phases.
                ModManager.Instance.Initialize();

                Phase = BootPhase.ModsReady;
                EnsureRuntime();

                Phase = BootPhase.Locale;
                LocaleLoader.LoadCurrentLanguage();

                Phase = BootPhase.Scenes;
                sceneManager?.Initialize();

                Phase = BootPhase.Ready;
                GameLogger.Log("BOOT", "Boot (sync) complete");
            }
            catch (OperationCanceledException)
            {
                GameLogger.Log("BOOT", "Boot (sync) cancelled", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                // Phase intentionally left where it failed: readiness guards stay
                // armed and every too-early consumer keeps logging loudly instead
                // of silently working against a half-initialized registry.
                GameLogger.Log("BOOT", $"Boot (sync) FAILED at phase {Phase}: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Launcher-driven boot. Yields one frame first so the boot overlay is on
        /// screen, runs path preparation on the main thread, discovery and Roslyn
        /// compilation on a background thread (cooperatively cancellable when the
        /// launcher scene dies), then the Godot-bound phases back on the main
        /// thread. Throws <see cref="OperationCanceledException"/> if cancelled.
        /// </summary>
        public static async Task RunLauncherAsync(Node context, CancellationToken cancellationToken)
        {
            if (!Begin(BootMode.LauncherAsync))
                return;

            // Let the boot overlay render one frame before the heavy phases,
            // mirroring the previous launcher-driven timing.
            await context.ToSignal(context.GetTree(), SceneTree.SignalName.ProcessFrame);

            try
            {
                Phase = BootPhase.PreparingPaths;
                BootProgress.EnterPhase(BootPhase.PreparingPaths);
                ModManager.Instance.PreparePaths();

                Phase = BootPhase.Discovering;
                BootProgress.EnterPhase(BootPhase.Discovering);
                await Task.Run(
                    () => ModManager.Instance.DiscoverAndCompile(cancellationToken),
                    cancellationToken);

                Phase = BootPhase.FinalizingMods;
                BootProgress.EnterPhase(BootPhase.FinalizingMods);
                ModRegistry.Instance.FinalizeGodotInit();

                Phase = BootPhase.ModsReady;
                BootProgress.EnterPhase(BootPhase.ModsReady);
                EnsureRuntime();

                Phase = BootPhase.Locale;
                BootProgress.EnterPhase(BootPhase.Locale);
                LocaleLoader.LoadCurrentLanguage();

                Phase = BootPhase.Scenes;
                BootProgress.EnterPhase(BootPhase.Scenes);
                context.GetNodeOrNull<SceneManager>("/root/SceneManager")?.Initialize();

                Phase = BootPhase.Ready;
                BootProgress.EnterPhase(BootPhase.Ready);
                GameLogger.Log("BOOT", "Boot (launcher) complete");
            }
            finally
            {
                // Phase intentionally left where the run stopped: on cancellation
                // readiness guards stay armed, so post-cancel consumers are loud.
            }
        }

        /// <summary>
        /// Constructs the session RuntimeData at a defined point: after the mod
        /// set is final, so the starter inventory is seeded by fully loaded
        /// factories. Replaces the old lazy getter on GameData.Runtime whose
        /// result silently depended on who touched it first.
        /// </summary>
        private static void EnsureRuntime()
        {
            var data = GameData.Instance;
            if (data == null)
            {
                GameLogger.Log("BOOT", "GameData autoload missing — RuntimeData not created", LogLevel.Error);
                return;
            }

            // A RuntimeData that slipped through before the mod set was final
            // (e.g. a timer tick racing the launcher boot) seeded its starter
            // inventory against an empty registry, and those skips are permanent.
            // Recreate it now that factories can resolve mod items.
            if (data.ConsumePrematureRuntimeCreation())
            {
                GameLogger.Log("BOOT",
                    "RuntimeData was created before mods were ready — recreating it so the starter inventory seeds from loaded mods",
                    LogLevel.Warning);
                data.ResetRuntime("created before boot finished; starter inventory was empty");
            }

            _ = data.Runtime;
        }

        /// <summary>Idempotence guard: exactly one boot per process.</summary>
        private static bool Begin(BootMode mode)
        {
            lock (startLock)
            {
                if (Mode != BootMode.None)
                {
                    GameLogger.Log("BOOT",
                        $"Run({mode}) ignored: boot already ran (mode={Mode}, phase={Phase})",
                        LogLevel.Warning);
                    return false;
                }

                Mode = mode;
                // Fresh service set for this boot: registries empty, the
                // reset list restored to the core defaults, factory slots unused.
                AppServices.SetCurrent(new AppServices());
                return true;
            }
        }
    }
}
