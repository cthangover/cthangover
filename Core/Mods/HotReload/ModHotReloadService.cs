namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Pipeline;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Menu;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Hot reload of mods inside the running game: edit a mod (C#, data,
    /// scenes, locale) and the game picks it up without a restart. File
    /// system watchers on the mod roots mark the set dirty; once the files
    /// have been quiet for <see cref="DebounceSeconds"/> (editors save in
    /// bursts), <see cref="ReloadNowAsync"/> runs. <c>Ctrl+Shift+R</c> triggers
    /// the same reload by hand (watchers may miss events on network or WSL
    /// mounts).
    ///
    /// A reload compiles the new set in the background first: on a compile
    /// error the game keeps running untouched and the errors are logged. On
    /// success the running session is parked in a hidden save slot
    /// (<see cref="SaveService.HotReloadSlot"/>), the window switches to the
    /// transition screen, the mod set is replaced (only changed mods and their
    /// dependents reload; see <see cref="ModReloadPlan"/>), and the session is
    /// restored: the save is loaded and its scene entered again, or the main
    /// menu reopens when no game was running.
    /// </summary>
    public partial class ModHotReloadService : Node
    {
        /// <summary>Quiet time after the last file event before a reload starts.</summary>
        public const double DebounceSeconds = 0.6;

        private readonly List<FileSystemWatcher> watchers = new();
        private readonly Stopwatch sinceLastChange = new();
        private int dirty;
        private string lastChangedPath;

        /// <summary>The installed service; null when hot reload is disabled for this process.</summary>
        public static ModHotReloadService Instance { get; private set; }

        /// <summary>
        /// Adds the service under <c>/root</c> when <see cref="HotReloadSettings.IsEnabled"/>
        /// allows it. Called once the boot finished.
        /// </summary>
        public static void InstallIfEnabled(SceneTree tree)
        {
            if (Instance != null || tree?.Root == null)
                return;
            if (!HotReloadSettings.IsEnabled(OS.HasFeature("editor"), OS.GetCmdlineUserArgs()))
                return;
            var service = new ModHotReloadService { Name = "ModHotReload" };
            ModRoot.AddDeferred(service);
        }

        /// <summary>Registers the instance and starts one recursive watcher per mod root resolved at boot.</summary>
        public override void _EnterTree()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            // A root that does not exist yet (user://mods before the first
            // download) is created, so mods dropped there later are seen too.
            foreach (var root in ModRegistry.Instance.RootPaths)
            {
                Directory.CreateDirectory(root);
                Watch(root);
            }
            GameLogger.Log("MODS_RELOAD", $"Hot reload: watching {watchers.Count} mod root(s); Ctrl+Shift+R reloads by hand");
        }

        /// <summary>Stops the watchers.</summary>
        public override void _ExitTree()
        {
            foreach (var watcher in watchers)
                watcher.Dispose();
            watchers.Clear();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Starts the reload on the main thread once the files have been quiet for <see cref="DebounceSeconds"/> and no other switch runs.</summary>
        public override void _Process(double delta)
        {
            if (Volatile.Read(ref dirty) == 0 || sinceLastChange.Elapsed.TotalSeconds < DebounceSeconds)
                return;
            if (GameSession.IsSwitching || ModManager.Instance.IsReloading || IsReloadRunning)
                return;
            Interlocked.Exchange(ref dirty, 0);
            GameLogger.Log("MODS_RELOAD", $"Hot reload: mod files changed ({lastChangedPath}), reloading");
            _ = ReloadNowAsync();
        }

        /// <summary>Ctrl+Shift+R: reload by hand.</summary>
        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R, CtrlPressed: true, ShiftPressed: true })
            {
                GetViewport().SetInputAsHandled();
                _ = ReloadNowAsync();
            }
        }

        /// <summary>True while <see cref="ReloadNowAsync"/> runs.</summary>
        public static bool IsReloadRunning { get; private set; }

        /// <summary>
        /// Reloads the current selection in place, preserving the session (see
        /// the class remarks). <paramref name="force"/> reloads mods even when
        /// their code did not change. Returns the verified result; a rejected
        /// reload (compile errors, another reload running) returns a result with
        /// <c>Applied == false</c> and leaves the game as it was.
        /// </summary>
        public static async Task<ModReloadResult> ReloadNowAsync(IReadOnlyCollection<string> force = null)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var manager = ModManager.Instance;
            if (tree == null || IsReloadRunning || GameSession.IsSwitching || manager.IsReloading)
                return null;

            IsReloadRunning = true;
            try
            {
                var prep = await manager.PrepareReloadAsync(GameLaunchArgs.Current, force);
                if (!prep.Succeeded)
                {
                    var rejected = manager.ApplyReload(prep, tree);
                    foreach (var error in rejected.Errors)
                        GameLogger.Log("MODS_RELOAD", "Hot reload rejected: " + error, LogLevel.Error);
                    return await manager.VerifyReloadAsync(rejected, tree);
                }

                var inGame = tree.CurrentScene?.IsInGroup("main_event_chain") == true && SceneManager.GetCurrentSceneName() != null;
                if (inGame)
                    SaveService.Save(SaveService.HotReloadSlot, rememberSession: false);

                ModSetTransitionScreen.Show(tree);
                // Let the engine free the previous scene before its mods go.
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                var result = manager.ApplyReload(prep, tree);
                await manager.VerifyReloadAsync(result, tree);

                if (inGame && GameSession.LoadAndEnter(tree.Root.GetNode("SceneManager"), SaveService.HotReloadSlot))
                    GameLogger.Log("MODS_RELOAD", $"Hot reload: session restored in scene '{SaveService.LastLoadedSceneName}'");
                else
                    GameSession.OpenMainMenu(tree);
                if (inGame)
                    SaveService.DeleteSave(SaveService.HotReloadSlot);
                return result;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_RELOAD", $"Hot reload FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                GameSession.OpenMainMenu(tree);
                return null;
            }
            finally
            {
                IsReloadRunning = false;
            }
        }

        private void Watch(string root)
        {
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                watcher.Changed += OnFileEvent;
                watcher.Created += OnFileEvent;
                watcher.Deleted += OnFileEvent;
                watcher.Renamed += OnFileRenamed;
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_RELOAD", $"Hot reload: cannot watch '{root}': {ex.Message}", LogLevel.Warning);
            }
        }

        // Watcher callbacks run on thread-pool threads: they only flip the
        // dirty flag; the reload itself starts from _Process on the main thread.
        private void OnFileEvent(object sender, FileSystemEventArgs e) => MarkDirty(e.FullPath);

        private void OnFileRenamed(object sender, RenamedEventArgs e) => MarkDirty(e.FullPath);

        private void MarkDirty(string path)
        {
            if (!HotReloadSettings.IsRelevantChange(path))
                return;
            lastChangedPath = path;
            sinceLastChange.Restart();
            Interlocked.Exchange(ref dirty, 1);
        }
    }
}
