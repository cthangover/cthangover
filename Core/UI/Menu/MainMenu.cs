using System.Collections.Generic;
using Cthangover.Core.Scenes;
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
    /// and loads BaseScene. The Tools button dynamically builds a
    /// tool-selection Window from ToolFactory at click time, avoiding
    /// hardcoding tool references. SettingsMenu and SaveLoadMenu are
    /// created at ready time but hidden.
    /// </summary>
    public partial class MainMenu : Control
    {
        private SettingsMenu _settingsMenu;
        private SaveLoadMenu _saveLoadMenu;
        private readonly Dictionary<Button, string> _btnKeys = new();

        public override void _Ready()
        {
            GetNode<Button>("MenuContainer/NewGameBtn").Pressed += OnNewGameClick;
            GetNode<Button>("MenuContainer/LoadBtn").Pressed += OnLoadClick;
            GetNode<Button>("MenuContainer/SettingsBtn").Pressed += OnSettingsClick;
            GetNode<Button>("MenuContainer/ExitBtn").Pressed += OnExitClick;
            GetNode<Button>("MenuContainer/ToolsBtn").Pressed += OnToolsClick;

            _btnKeys[GetNode<Button>("MenuContainer/NewGameBtn")] = "menu/newgame";
            _btnKeys[GetNode<Button>("MenuContainer/LoadBtn")] = "ui/menu/load";
            _btnKeys[GetNode<Button>("MenuContainer/SettingsBtn")] = "menu/settings";
            _btnKeys[GetNode<Button>("MenuContainer/ExitBtn")] = "menu/exit";
            _btnKeys[GetNode<Button>("MenuContainer/ToolsBtn")] = "menu/tools";

            _settingsMenu = GD.Load<PackedScene>("res://scenes/SettingsMenu.tscn").Instantiate<SettingsMenu>();
            _settingsMenu.Visible = false;
            _settingsMenu.LanguageChanged += RefreshButtons;
            AddChild(_settingsMenu);

            _saveLoadMenu = GD.Load<PackedScene>("res://scenes/SaveLoadMenu.tscn").Instantiate<SaveLoadMenu>();
            _saveLoadMenu.Visible = false;
            AddChild(_saveLoadMenu);
            
            RefreshButtons();

            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            sceneManager.Initialize();

            CheckModCompilationErrors();
        }

        private void CheckModCompilationErrors()
        {
            if (GameLogger.CompilationErrors.Count == 0)
                return;

            var errorText = string.Join("\r\n", GameLogger.CompilationErrors);
            ShowModErrorPopup(errorText);
        }

        private void OnToolsClick()
        {
            var panel = new Window();
            panel.Title = TranslationServer.Translate("tools/title");
            panel.Unresizable = true;
            panel.Size = new Vector2I(300, 160);
            panel.CloseRequested += () => panel.QueueFree();

            var vbox = new VBoxContainer();
            vbox.AnchorRight = 1f;
            vbox.AnchorBottom = 1f;
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            panel.AddChild(vbox);

            vbox.AddChild(new Label { Text = TranslationServer.Translate("tools/select_tool") });

            var dropdown = new OptionButton();
            var tools = ToolFactory.Instance.GetAll();
            foreach (var tool in tools)
            {
                dropdown.AddItem(TranslationServer.Translate(tool.LocaleKey));
                dropdown.SetItemMetadata(dropdown.ItemCount - 1, tool.Id);
            }
            dropdown.Selected = 0;
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddChild(dropdown);

            var btnHBox = new HBoxContainer();
            btnHBox.Alignment = BoxContainer.AlignmentMode.End;
            vbox.AddChild(btnHBox);

            var launchBtn = new Button { Text = TranslationServer.Translate("tools/launch") };
            launchBtn.Pressed += () =>
            {
                var meta = dropdown.GetItemMetadata(dropdown.Selected).AsString();
                var tool = ToolFactory.Instance.Get(meta);
                if (tool != null)
                    ToolWindow.ShowWindow(tool.CreateWindow());
                panel.QueueFree();
            };
            btnHBox.AddChild(launchBtn);

            var closeBtn = new Button { Text = TranslationServer.Translate("tools/close") };
            closeBtn.Pressed += () => panel.QueueFree();
            btnHBox.AddChild(closeBtn);

            AddChild(panel);
            panel.PopupCentered();
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
            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            if (sceneManager != null)
            {
                sceneManager.PendingSceneName = "start_scene";
                var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
                sceneService?.LoadScene("res://Scenes/BaseScene.tscn");
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

    }

}
