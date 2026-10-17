using System.Collections.Generic;
using Cthangover.Core.Scenes;
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
    /// game restarts the process, because the boot decides the mod set.
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

        /// <summary>
        /// Restarts the process into <paramref name="gameId"/>: a new game, or
        /// <paramref name="loadSave"/> of that game. Engine args are kept, user
        /// args are replaced.
        /// </summary>
        public static void RestartInto(Node host, string gameId, string loadSave = null)
        {
            var args = new List<string>(OS.GetCmdlineArgs());
            args.Add("--");
            args.AddRange(GameLaunchArgs.Build(gameId, loadSave));
            GameLogger.Log("GAME", $"Restarting into game '{gameId}' ({(loadSave == null ? "new game" : "load " + loadSave)})");
            OS.SetRestartOnExit(true, args.ToArray());
            host.GetTree().Quit();
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
