using System;
using Cthangover.Core.Games;
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
                Id = "continue",
                ModId = ModId,
                Section = "game",
                Locale = "menu/continue",
                Variation = "primary",
                Index = 5,
                Visible = GameFlow.HasContinue,
                Handler = BaseMenuActions.Continue,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "play",
                ModId = ModId,
                Section = "game",
                Locale = "menu/play",
                Variation = "primary",
                Index = 10,
                Handler = BaseMenuActions.Play,
            });
            // A new game of the running game; from the front-end games start
            // from the picker instead.
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "new_game",
                ModId = ModId,
                Section = "game",
                Locale = "menu/newgame",
                Index = 15,
                Visible = () => !ActiveGame.IsFrontendOnly,
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
        /// Starts a new game at the active game's entry scene (<c>start_scene</c>
        /// when no game collection is active) through <see cref="GameSession"/>.
        /// </summary>
        public static void NewGame(Control host)
        {
            GameSession.StartNew(host);
        }

        /// <summary>Loads the last save of the last played game (restarting into it when needed).</summary>
        public static void Continue(Control host) => GameFlow.Continue(host);

        /// <summary>Opens the game picker.</summary>
        public static void Play(Control host)
        {
            host.GetNodeOrNull<GamePickerPanel>("GamePickerPanel")?.Open();
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
