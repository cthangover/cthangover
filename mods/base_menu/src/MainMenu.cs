using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Menu;
namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Title screen main menu, fully driven by <see cref="MenuRegistry"/>. The scene
    /// only defines named section containers (game, mods, exit, tools); every button —
    /// built-in or mod-provided — is created from registry entries, so mods can add,
    /// replace or re-order entries without touching this scene. Below the sections,
    /// every registered tool appends its own launcher button via
    /// <see cref="IToolProvider.AddMenuButton"/>, so no tool list is hardcoded here.
    /// If GameLogger reports mod compilation errors, a popup lists them. The
    /// SettingsMenu and SaveLoadMenu panels are instantiated at ready time but stay
    /// hidden; they are registered in <see cref="MenuRegistry"/> as panels "settings"
    /// and "save_load", so other mods can open them by id.
    /// </summary>
    public partial class MainMenu : Control
    {
        private SettingsMenu _settingsMenu;
        private SaveLoadMenu _saveLoadMenu;
        private GamePickerPanel _gamePicker;
        private readonly Dictionary<Button, string> _btnKeys = new();

        public override void _Ready()
        {
            try
            {
                BuildUi();
            }
            catch (System.Exception ex)
            {
                GameLogger.Log("MAIN_MENU",
                    $"_Ready failed: {ex.GetType().Name}: {ex.Message}{System.Environment.NewLine}{ex.StackTrace}",
                    LogLevel.Error);
                throw;
            }
        }



        /// <summary>
        /// Assembles the runtime UI: theme, hidden settings and save/load panels,
        /// telegram link wiring, scene manager initialization and registry/tool
        /// driven buttons.
        /// </summary>
        private void BuildUi()
        {
            CthangoverTheme.Apply(this);

            _settingsMenu = new ModScenePrefab("scenes/menu/settings_menu.tscn").Instantiate<SettingsMenu>();
            _settingsMenu.Name = "SettingsPanel";
            _settingsMenu.Visible = false;
            _settingsMenu.LanguageChanged += RefreshButtons;
            AddChild(_settingsMenu);

            _saveLoadMenu = new ModScenePrefab("scenes/menu/save_load_menu.tscn").Instantiate<SaveLoadMenu>();
            _saveLoadMenu.Name = "SaveLoadPanel";
            _saveLoadMenu.Visible = false;
            AddChild(_saveLoadMenu);

            _gamePicker = new GamePickerPanel { Name = "GamePickerPanel" };
            AddChild(_gamePicker);
            _gamePicker.SavesRequested += gameId =>
            {
                _gamePicker.Visible = false;
                _saveLoadMenu.OpenForGame(gameId);
            };

            var telegramLink = GetNode<RichTextLabel>("TelegramLink");
            telegramLink.MetaClicked += OnTelegramLinkClicked;

            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            sceneManager.Initialize();

            BuildRegistryButtons();
            BuildToolButtons();
            CheckModCompilationErrors();

            RefreshButtons();
        }

        /// <summary>
        /// Creates one button per <see cref="MenuRegistry"/> entry, routed to the
        /// section container matching its <c>Section</c> ("game", "mods", "exit",
        /// "tools"). Runs after <see cref="SceneManager.Initialize"/> because the
        /// registry is built during boot and filled by mod initializers; the
        /// defensive Initialize call keeps direct scene launches working. Each
        /// button's translation key is registered so <see cref="RefreshButtons"/>
        /// keeps it localized.
        /// </summary>
        private void BuildRegistryButtons()
        {
            var sections = new Dictionary<string, Container>
            {
                { "game", GetNode<Container>("MenuContainer/GameButtons") },
                { MenuButtonEntry.SectionMods, GetNode<Container>("MenuContainer/ModMenuButtons") },
                { "exit", GetNode<Container>("MenuContainer/ExitButtons") },
                { "tools", GetNode<Container>("MenuContainer/ToolMenuButtons") },
            };
            MenuBuilder.Build(this, sections);

            foreach (var container in sections.Values)
                MenuBuilder.CollectLocaleKeys(container, _btnKeys);
        }

        /// <summary>
        /// Lets every registered tool append its launcher button to the tool block
        /// below Exit (<c>MenuContainer/ToolMenuButtons</c>). Tools register
        /// themselves in <see cref="ToolFactory"/>, so the block stays empty when no
        /// tools are installed and needs no changes when a mod introduces a new tool.
        /// Each button carries its translation key in metadata, keeping
        /// <see cref="RefreshButtons"/> aware of it.
        /// </summary>
        private void BuildToolButtons()
        {
            var container = GetNodeOrNull<VBoxContainer>("MenuContainer/ToolMenuButtons");
            if (container == null)
                return;

            foreach (var tool in ToolFactory.Instance.GetAll())
                tool.AddMenuButton(container);

            MenuBuilder.CollectLocaleKeys(container, _btnKeys);

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

        private void OnTelegramLinkClicked(Variant meta)
        {
            OS.ShellOpen($"https://{meta.AsString()}");
        }
    }
}


