using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Registers the built-in main-menu buttons and the settings/save-load panels
    /// in <see cref="MenuRegistry"/>. Runs from
    /// <c>ModInitializerRegistry.NotifyResourcesReady</c>, which fires after the
    /// registry itself is built, so these entries are appended to live registry
    /// state rather than collected from a manifest.
    /// </summary>
    public class BaseMenuInitializer : IModInitializer
    {
        private const string ModId = "base_menu";

        public void OnModLoaded(string modId)
        {
        }

        public void OnModResourcesReady()
        {
            MenuRegistry.RegisterMenuPanel(new MenuPanelEntry
            {
                Id = "settings",
                ModId = ModId,
                ScenePath = "scenes/menu/settings_menu.tscn",
                Locale = "menu/settings",
            });
            MenuRegistry.RegisterMenuPanel(new MenuPanelEntry
            {
                Id = "save_load",
                ModId = ModId,
                ScenePath = "scenes/menu/save_load_menu.tscn",
            });

            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "new_game",
                ModId = ModId,
                Section = "game",
                Locale = "menu/newgame",
                Variation = "primary",
                Index = 10,
                Handler = BaseMenuActions.NewGame,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "load",
                ModId = ModId,
                Section = "game",
                Locale = "ui/menu/load",
                Index = 20,
                Handler = BaseMenuActions.Load,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "settings",
                ModId = ModId,
                Section = "game",
                Locale = "menu/settings",
                Index = 30,
                Handler = BaseMenuActions.Settings,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "exit",
                ModId = ModId,
                Section = "exit",
                Locale = "menu/exit",
                Variation = "danger",
                Index = 1000,
                Handler = BaseMenuActions.Exit,
            });
        }
    }

    /// <summary>
    /// Handlers for the built-in menu buttons. Each receives the menu root control
    /// so it can locate the panels instantiated as named children by
    /// <see cref="MainMenu"/>.
    /// </summary>
    internal static class BaseMenuActions
    {
        /// <summary>
        /// Starts a new game: resets <see cref="GameData"/>, sets
        /// <see cref="SceneManager.PendingSceneName"/> to "start_scene" and loads
        /// the shell via <see cref="GodotSceneService"/>.
        /// </summary>
        public static void NewGame(Control host)
        {
            GameData.Instance?.StartNewGame();
            var sceneManager = host.GetNode<SceneManager>("/root/SceneManager");
            if (sceneManager != null)
            {
                sceneManager.PendingSceneName = "start_scene";
                var sceneService = host.GetNode<GodotSceneService>("/root/GodotSceneService");
                sceneService?.LoadShell();
            }
        }

        /// <summary>Opens the save/load panel in load mode.</summary>
        public static void Load(Control host)
        {
            host.GetNodeOrNull<SaveLoadMenu>("SaveLoadPanel")?.OpenForLoad();
        }

        /// <summary>Shows the settings overlay.</summary>
        public static void Settings(Control host)
        {
            host.GetNodeOrNull<SettingsMenu>("SettingsPanel")?.Open();
        }

        /// <summary>Quits the application immediately via <c>SceneTree.Quit</c>.</summary>
        public static void Exit(Control host)
        {
            host.GetTree().Quit();
        }
    }
}
