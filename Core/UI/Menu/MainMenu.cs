using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Title screen main menu. On _Ready, initializes SceneManager
    /// (triggering mod discovery and IModInitializer callbacks), then
    /// checks GameLogger.CompilationErrors and shows a popup if any mods
    /// failed to compile. New Game sets the pending scene to "start_scene"
    /// and loads BaseScene. The buttons form two blocks: game actions (with
    /// mod-provided buttons collected by <see cref="MainMenuRegistry"/>) and,
    /// below Exit, tool launchers — every tool appends its own button via
    /// <see cref="IToolProvider.AddMenuButton"/>, so no tool list is
    /// hardcoded here. SettingsMenu and SaveLoadMenu are created at ready
    /// time but hidden.
    /// </summary>
    public partial class MainMenu : Control
    {
        private SettingsMenu _settingsMenu;
        private SaveLoadMenu _saveLoadMenu;
        private readonly Dictionary<Button, string> _btnKeys = new();

        public override void _Ready()
        {
            CthangoverTheme.Apply(this);

            GetNode<Button>("MenuContainer/NewGameBtn").Pressed += OnNewGameClick;
            GetNode<Button>("MenuContainer/LoadBtn").Pressed += OnLoadClick;
            GetNode<Button>("MenuContainer/SettingsBtn").Pressed += OnSettingsClick;
            GetNode<Button>("MenuContainer/ExitBtn").Pressed += OnExitClick;

            var telegramLink = GetNode<RichTextLabel>("TelegramLink");
            telegramLink.MetaClicked += OnTelegramLinkClicked;

            _btnKeys[GetNode<Button>("MenuContainer/NewGameBtn")] = "menu/newgame";
            _btnKeys[GetNode<Button>("MenuContainer/LoadBtn")] = "ui/menu/load";
            _btnKeys[GetNode<Button>("MenuContainer/SettingsBtn")] = "menu/settings";
            _btnKeys[GetNode<Button>("MenuContainer/ExitBtn")] = "menu/exit";

            _settingsMenu = new ModScenePrefab("scenes/menu/settings_menu.tscn").Instantiate<SettingsMenu>();
            _settingsMenu.Visible = false;
            _settingsMenu.LanguageChanged += RefreshButtons;
            AddChild(_settingsMenu);

            _saveLoadMenu = new ModScenePrefab("scenes/menu/save_load_menu.tscn").Instantiate<SaveLoadMenu>();
            _saveLoadMenu.Visible = false;
            AddChild(_saveLoadMenu);
            
            RefreshButtons();

            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            sceneManager.Initialize();

            BuildModButtons();
            BuildToolButtons();
            CheckModCompilationErrors();
        }

        /// <summary>
        /// Materializes buttons declared by enabled mods in their manifest's
        /// <c>main_menu</c> section. Runs after <see cref="SceneManager.Initialize"/>
        /// because the registry reads the mod list; each button's translation key is
        /// registered so <see cref="RefreshButtons"/> keeps it localized.
        /// </summary>
        private void BuildModButtons()
        {
            var container = GetNodeOrNull<VBoxContainer>("MenuContainer/ModMenuButtons");
            if (container == null)
                return;

            MainMenuBuilder.Build(this, container);

            foreach (var child in container.GetChildren())
            {
                if (child is Button button && button.HasMeta(MainMenuBuilder.LocaleMetaKey))
                    _btnKeys[button] = button.GetMeta(MainMenuBuilder.LocaleMetaKey).AsString();
            }
        }

        /// <summary>
        /// Lets every registered tool append its launcher button to the tool block
        /// below Exit (<c>MenuContainer/ToolMenuButtons</c>). Tools register
        /// themselves in <see cref="ToolFactory"/>, so the block stays empty when no
        /// tools are installed and needs no changes when a mod introduces a new tool.
        /// Runs after <see cref="SceneManager.Initialize"/> so mod assemblies are
        /// already registered. Each button carries its translation key in metadata,
        /// keeping <see cref="RefreshButtons"/> aware of it.
        /// </summary>
        private void BuildToolButtons()
        {
            var container = GetNodeOrNull<VBoxContainer>("MenuContainer/ToolMenuButtons");
            if (container == null)
                return;

            foreach (var tool in ToolFactory.Instance.GetAll())
                tool.AddMenuButton(container);

            foreach (var child in container.GetChildren())
            {
                if (child is Button button && button.HasMeta(MainMenuBuilder.LocaleMetaKey))
                    _btnKeys[button] = button.GetMeta(MainMenuBuilder.LocaleMetaKey).AsString();
            }

            GameLogger.Log("MAIN_MENU", $"Built {container.GetChildCount()} tool button(s)");
        }

        private void CheckModCompilationErrors()
        {
            if (GameLogger.CompilationErrors.Count == 0)
                return;

            var errorText = string.Join("\r\n", GameLogger.CompilationErrors);
            ShowModErrorPopup(errorText);
        }

        private void RefreshButtons()
        {
            foreach (var kv in _btnKeys)
                kv.Key.Text = TranslationServer.Translate(kv.Value);
        }

        /// <summary>
        /// Starts a new game by setting <see cref="SceneManager.PendingSceneName"/> to "start_scene"
        /// and loading <c>BaseScene.tscn</c> via <see cref="GodotSceneService"/>. The scene manager
        /// picks up the pending scene name during its initialization flow.
        /// </summary>
        public void OnNewGameClick()
        {
            GameData.Instance?.StartNewGame();
            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            if (sceneManager != null)
            {
                sceneManager.PendingSceneName = "start_scene";
                var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
                sceneService?.LoadShell();
            }
        }

        private void ShowModErrorPopup(string errorText)
        {
            var dialog = new AcceptDialog();
            dialog.Title = "Mod Compilation Error";
            dialog.DialogText = errorText;
            dialog.OkButtonText = "OK";
            dialog.Exclusive = true;
            AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>
        /// Opens the <see cref="SaveLoadMenu"/> in load mode, displaying saved game slots
        /// for the player to pick from.
        /// </summary>
        public void OnLoadClick()
        {
            _saveLoadMenu.OpenForLoad();
        }

        /// <summary>
        /// Shows the <see cref="SettingsMenu"/> overlay. The menu was already instantiated
        /// during <c>_Ready</c> so there is no loading delay.
        /// </summary>
        public void OnSettingsClick()
        {
            _settingsMenu.Visible = true;
        }

        /// <summary>
        /// Quits the application immediately via <c>SceneTree.Quit</c>.
        /// </summary>
        public void OnExitClick()
        {
            GetTree().Quit();
        }

        private void OnTelegramLinkClicked(Variant meta)
        {
            OS.ShellOpen($"https://{meta.AsString()}");
        }

    }

}
