using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Games
{
    /// <summary>
    /// Starting, loading and switching games, shared by the launcher (args
    /// after boot) and the menu mods. Starting enters the active game's
    /// entry scene; loading falls back to it when the saved scene no longer
    /// exists (its mechanic was removed from the game). Switching to another
    /// game replaces the mod set in-process (<see cref="SwitchToAsync"/>); the
    /// menu warms the mod sets of the games up in the background beforehand
    /// (<see cref="WarmUp"/>), so the switch only loads cached assemblies.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Resets the runtime and enters <see cref="ActiveGame.EntryScene"/> through the base shell.</summary>
        public static void StartNew(Node host)
        {
            GameData.Instance?.StartNewGame();
            EnterScene(host, ActiveGame.EntryScene);
        }

        /// <summary>
        /// Loads <paramref name="fileName"/> from the active game's saves and
        /// enters the saved scene (or the entry scene when the saved one is
        /// unknown). Returns false when the save could not be loaded.
        /// </summary>
        public static bool LoadAndEnter(Node host, string fileName)
        {
            if (!SaveService.Load(fileName))
                return false;

            var sceneManager = host.GetNodeOrNull<SceneManager>("/root/SceneManager");
            sceneManager?.Initialize();

            var scene = SaveService.LastLoadedSceneName;
            if (string.IsNullOrEmpty(scene) || scene == "unknown" || sceneManager?.HasScene(scene) != true)
            {
                if (!string.IsNullOrEmpty(scene) && scene != "unknown")
                    GameLogger.Log("SAVE", $"Saved scene '{scene}' is not part of the game any more, entering '{ActiveGame.EntryScene}'", LogLevel.Warning);
                scene = ActiveGame.EntryScene;
            }

            EnterScene(host, scene);
            return true;
        }

        /// <summary>True while <see cref="SwitchToAsync"/> runs; menus ignore further switch requests meanwhile.</summary>
        public static bool IsSwitching { get; private set; }

        private static CancellationTokenSource warmUpCancel;
        private static int warmUpRuns;

        /// <summary>True while a background warm-up (<see cref="WarmUp"/>) is compiling.</summary>
        public static bool IsWarmingUp => Volatile.Read(ref warmUpRuns) > 0;

        /// <summary>
        /// Compiles the mod sets of <paramref name="gameIds"/> into the assembly
        /// cache in the background, one game after another, while the player is
        /// in the menu (<see cref="ModManager.WarmUp"/>). Picking a game then only
        /// loads cached assemblies instead of compiling them behind the loading
        /// screen. A running warm-up is replaced; <see cref="SwitchToAsync"/>
        /// cancels it, and the switch finishes whatever mod it was compiling.
        /// </summary>
        public static void WarmUp(IReadOnlyList<string> gameIds)
        {
            StopWarmUp();
            if (gameIds == null || gameIds.Count == 0)
                return;

            var cancel = new CancellationTokenSource();
            warmUpCancel = cancel;
            var args = GameLaunchArgs.Current;
            var manager = ModManager.Instance;
            Interlocked.Increment(ref warmUpRuns);
            Task.Run(() => WarmUpGames(manager, args, new List<string>(gameIds), cancel.Token));
        }

        /// <summary>Stops the background warm-up between two mods; already compiled mods stay in the cache.</summary>
        public static void StopWarmUp()
        {
            warmUpCancel?.Cancel();
            warmUpCancel = null;
        }

        private static void WarmUpGames(ModManager manager, GameLaunchArgs args, List<string> gameIds, CancellationToken cancellationToken)
        {
            try
            {
                WarmUpEach(manager, args, gameIds, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref warmUpRuns);
            }
        }

        private static void WarmUpEach(ModManager manager, GameLaunchArgs args, List<string> gameIds, CancellationToken cancellationToken)
        {
            foreach (var gameId in gameIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                var watch = Stopwatch.StartNew();
                try
                {
                    manager.WarmUp(args.WithSelection(gameId, null), cancellationToken);
                    GameLogger.Log("GAME", $"Warm-up: mods of '{gameId}' are in the assembly cache ({watch.ElapsedMilliseconds} ms)");
                }
                catch (OperationCanceledException)
                {
                    GameLogger.Log("GAME", $"Warm-up of '{gameId}' stopped: a game is starting");
                    return;
                }
                catch (Exception ex)
                {
                    GameLogger.Log("GAME", $"Warm-up of '{gameId}' failed: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// Switches the running process to <paramref name="gameId"/> — a new
        /// game, or <paramref name="loadSave"/> of it — without a restart: the
        /// window shows <see cref="ModSetTransitionScreen"/>, the game's mod set
        /// is prepared (compiled) in the background, mods that leave the set are
        /// unloaded and their contexts released, the new ones load, and the game
        /// is entered exactly as a cold <c>--game</c> launch would. Mods shared by
        /// both sets (the front-end, common engine mods) stay loaded. When the new
        /// set does not compile, the running set is untouched and the screen
        /// lists the errors with a way back to the main menu. Returns whether the
        /// game was entered.
        /// </summary>
        public static async Task<bool> SwitchToAsync(Node host, string gameId, string loadSave = null)
        {
            var tree = host.GetTree();
            var manager = ModManager.Instance;
            if (IsSwitching || manager.IsReloading)
            {
                GameLogger.Log("GAME", $"Switch into '{gameId}' ignored: a mod-set switch is already running", LogLevel.Warning);
                return false;
            }

            IsSwitching = true;
            StopWarmUp();
            try
            {
                GameLogger.Log("GAME", $"Switching into game '{gameId}' ({(loadSave == null ? "new game" : "load " + loadSave)}) in-process");
                var selection = GameLaunchArgs.Current.WithSelection(gameId, loadSave);
                var screen = ModSetTransitionScreen.Show(tree);

                var prep = await manager.PrepareReloadAsync(selection);
                var result = manager.ApplyReload(prep, tree);
                if (!result.Applied)
                {
                    screen.ShowErrors(result.Errors, OpenMainMenuDeferred);
                    return false;
                }

                await manager.VerifyReloadAsync(result, tree);

                var anchor = tree.Root.GetNodeOrNull<SceneManager>("SceneManager");
                if (string.IsNullOrEmpty(loadSave))
                {
                    StartNew(anchor);
                    return true;
                }

                if (LoadAndEnter(anchor, loadSave))
                    return true;

                GameLogger.Log("GAME", $"Save '{loadSave}' of game '{gameId}' could not be loaded", LogLevel.Error);
                OpenMainMenu(tree);
                return false;
            }
            catch (Exception ex)
            {
                GameLogger.Log("GAME", $"Switch into '{gameId}' FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                OpenMainMenu(tree);
                return false;
            }
            finally
            {
                IsSwitching = false;
            }
        }

        /// <summary>
        /// Enters the title screen. The menu scene is provided by the
        /// <c>menu_base</c> mod and resolved through the runtime tscn pipeline
        /// (which also applies scene patches); if even that cannot be built, the
        /// engine loader runs anyway so a broken mod set degrades to a loud log
        /// instead of a crash.
        /// </summary>
        public static void OpenMainMenu(SceneTree tree)
        {
            var path = GodotSceneService.GetScenePath(GodotSceneType.MainMenu);
            var node = TscnScenes.LoadAndBuild(path);
            if (node != null)
            {
                tree.ChangeSceneToNode(node);
                return;
            }

            GameLogger.Log("LAUNCHER", $"main menu scene '{path}' could not be built by any mod — falling back to the native loader", LogLevel.Warning);
            tree.ChangeSceneToFile("res://" + path);
        }

        private static void OpenMainMenuDeferred()
        {
            if (Engine.GetMainLoop() is SceneTree tree)
                OpenMainMenu(tree);
        }

        private static void EnterScene(Node host, string scene)
        {
            var sceneManager = host.GetNodeOrNull<SceneManager>("/root/SceneManager");
            if (sceneManager == null)
            {
                GameLogger.Log("GAME", "SceneManager autoload not found, cannot enter the game", LogLevel.Error);
                return;
            }

            sceneManager.PendingSceneName = scene;
            host.GetNodeOrNull<GodotSceneService>("/root/GodotSceneService")?.LoadShell();
        }
    }
}
