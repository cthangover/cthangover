using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Tool.LightEditor;
using Cthangover.Core.UI.Tool.SceneBuilder;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{

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
            dropdown.AddItem(TranslationServer.Translate("tools/light_editor/title"));
            dropdown.SetItemMetadata(0, "light_editor");
            dropdown.AddItem(TranslationServer.Translate("tools/scene_builder/title"));
            dropdown.SetItemMetadata(1, "scene_builder");
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
                if (meta == "light_editor")
                    LightEditorWindow.Open();
                else if (meta == "scene_builder")
                    SceneBuilderWindow.Open();
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

        public void OnNewGameClick()
        {
            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            if (sceneManager != null)
            {
                sceneManager.Initialize();
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

        public void OnLoadClick()
        {
            _saveLoadMenu.OpenForLoad();
        }

        public void OnSettingsClick()
        {
            _settingsMenu.Visible = true;
        }

        public void OnExitClick()
        {
            GetTree().Quit();
        }

    }

}
