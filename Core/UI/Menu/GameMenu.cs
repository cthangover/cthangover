using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    public partial class GameMenu : Control
    {
        private SettingsMenu _settingsMenu;
        private SaveLoadMenu _saveLoadMenu;
        private Button _saveBtn;
        private readonly Dictionary<Button, string> _btnKeys = new();

        public override void _Ready()
        {
            _saveBtn = GetNode<Button>("Panel/Margin/VBox/SaveBtn");
            _saveBtn.Pressed += OnSaveClick;

            GetNode<Button>("Panel/Margin/VBox/ReturnBtn").Pressed += OnReturnClick;
            GetNode<Button>("Panel/Margin/VBox/LoadBtn").Pressed += OnLoadClick;
            GetNode<Button>("Panel/Margin/VBox/SettingsBtn").Pressed += OnSettingsClick;
            GetNode<Button>("Panel/Margin/VBox/MainMenuBtn").Pressed += OnMainMenuClick;
            GetNode<Button>("Panel/Margin/VBox/ExitBtn").Pressed += OnExitClick;

            _btnKeys[GetNode<Button>("Panel/Margin/VBox/ReturnBtn")] = "menu/resume";
            _btnKeys[_saveBtn] = "menu/save";
            _btnKeys[GetNode<Button>("Panel/Margin/VBox/LoadBtn")] = "menu/load";
            _btnKeys[GetNode<Button>("Panel/Margin/VBox/SettingsBtn")] = "menu/settings";
            _btnKeys[GetNode<Button>("Panel/Margin/VBox/MainMenuBtn")] = "menu/main_menu";
            _btnKeys[GetNode<Button>("Panel/Margin/VBox/ExitBtn")] = "menu/exit";

            _settingsMenu = GD.Load<PackedScene>("res://scenes/SettingsMenu.tscn").Instantiate<SettingsMenu>();
            _settingsMenu.Visible = false;
            _settingsMenu.LanguageChanged += RefreshButtons;
            AddChild(_settingsMenu);

            _saveLoadMenu = GD.Load<PackedScene>("res://scenes/SaveLoadMenu.tscn").Instantiate<SaveLoadMenu>();
            _saveLoadMenu.Visible = false;
            AddChild(_saveLoadMenu);

            Visible = false;
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            foreach (var kv in _btnKeys)
                kv.Key.Text = TranslationServer.Translate(kv.Value);
        }

        private void OnReturnClick()
        {
            Visible = false;
        }

        private void OnSaveClick()
        {
            _saveLoadMenu.OpenForSave();
        }

        private bool IsSaveAllowed()
        {
            return SceneManager.IsSaveAllowedForCurrentScene();
        }

        public void RefreshSaveButton()
        {
            _saveBtn.Disabled = !IsSaveAllowed();
        }

        private void OnLoadClick()
        {
            _saveLoadMenu.OpenForLoad();
        }

        private void OnSettingsClick()
        {
            _settingsMenu.Visible = true;
        }

        private void OnMainMenuClick()
        {
            var sceneService = SceneContextNode.FindNode<GodotSceneService>("GodotSceneService");
            sceneService?.SwitchToMenu();
        }

        private void OnExitClick()
        {
            GetTree().Quit();
        }
    }
}
