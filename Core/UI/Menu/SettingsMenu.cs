using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    public partial class SettingsMenu : Control
    {
        [Signal]
        public delegate void LanguageChangedEventHandler();
        private TabContainer _tabs;
        private CheckButton _soundsToggle;
        private CheckButton _musicToggle;
        private CheckButton _ambientToggle;
        private HSlider _soundsVolume;
        private HSlider _musicVolume;
        private HSlider _ambientVolume;
        private Label _soundsVolumeValue;
        private Label _musicVolumeValue;
        private Label _ambientVolumeValue;
        private OptionButton _langSelect;
        private Button _backBtn;

        private readonly Dictionary<Label, string> _labelKeys = new();
        private bool _initialized;

        public override void _Ready()
        {
            _tabs = GetNode<TabContainer>("Panel/Margin/VBox/Tabs");

            _langSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/General/LangBox/LangSelect");

            _soundsToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/ToggleRow/SoundsToggle");
            _soundsVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/VolumeRow/SoundsVolume");
            _soundsVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/VolumeRow/SoundsVolumeValue");

            _musicToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/Audio/MusicBox/ToggleRow2/MusicToggle");
            _musicVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/Audio/MusicBox/VolumeRow2/MusicVolume");
            _musicVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/MusicBox/VolumeRow2/MusicVolumeValue");

            _ambientToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/ToggleRow3/AmbientToggle");
            _ambientVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/VolumeRow3/AmbientVolume");
            _ambientVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/VolumeRow3/AmbientVolumeValue");

            _backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");

            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Title"), "menu/settings");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/General/LangBox/LangLabel"), "settings/language");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/ToggleRow/SoundsLabel"), "settings/sounds");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/VolumeRow/SoundsVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/MusicBox/ToggleRow2/MusicLabel"), "settings/music");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/MusicBox/VolumeRow2/MusicVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/ToggleRow3/AmbientLabel"), "settings/ambient");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/VolumeRow3/AmbientVolumeLabel"), "settings/volume");

            _soundsToggle.Toggled += OnSoundsToggled;
            _musicToggle.Toggled += OnMusicToggled;
            _ambientToggle.Toggled += OnAmbientToggled;
            _soundsVolume.ValueChanged += OnSoundsVolumeChanged;
            _musicVolume.ValueChanged += OnMusicVolumeChanged;
            _ambientVolume.ValueChanged += OnAmbientVolumeChanged;
            _langSelect.ItemSelected += OnLanguageSelected;
            _backBtn.Pressed += OnBackPressed;
            _backBtn.Text = TranslationServer.Translate("settings/back");

            LoadSettingsToUI();
            _initialized = true;
        }

        private void RegisterLabel(Label label, string key)
        {
            if (label != null)
                _labelKeys[label] = key;
        }

        private void RefreshUIText()
        {
            foreach (var kv in _labelKeys)
                kv.Key.Text = TranslationServer.Translate(kv.Value);
            _backBtn.Text = TranslationServer.Translate("settings/back");
            _tabs.SetTabTitle(0, TranslationServer.Translate("settings/tab_general"));
            _tabs.SetTabTitle(1, TranslationServer.Translate("settings/tab_audio"));
            PopulateLangOptions();
        }

        private void LoadSettingsToUI()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            _soundsToggle.ButtonPressed = s.SoundsEnabled;
            _musicToggle.ButtonPressed = s.MusicsEnabled;
            _ambientToggle.ButtonPressed = s.AmbientEnabled;
            _soundsVolume.Value = s.SoundsVolume;
            _musicVolume.Value = s.MusicsVolume;
            _ambientVolume.Value = s.AmbientVolume;
            _soundsVolumeValue.Text = $"{s.SoundsVolume}%";
            _musicVolumeValue.Text = $"{s.MusicsVolume}%";
            _ambientVolumeValue.Text = $"{s.AmbientVolume}%";

            UpdateSlidersEditable();
            PopulateLangOptions();
            RefreshUIText();
        }

        private void PopulateLangOptions()
        {
            _langSelect.Clear();
            _langSelect.AddItem(TranslationServer.Translate("settings/lang_ru"));
            _langSelect.AddItem(TranslationServer.Translate("settings/lang_en"));
            _langSelect.AddItem(TranslationServer.Translate("settings/lang_zh"));

            var s = GameData.Instance?.Settings;
            if (s == null) return;
            _langSelect.Selected = s.Language switch { "en" => 1, "zh" => 2, _ => 0 };
        }

        private void ApplyAndSave()
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            s.SoundsEnabled = _soundsToggle.ButtonPressed;
            s.MusicsEnabled = _musicToggle.ButtonPressed;
            s.AmbientEnabled = _ambientToggle.ButtonPressed;
            s.SoundsVolume = (int)_soundsVolume.Value;
            s.MusicsVolume = (int)_musicVolume.Value;
            s.AmbientVolume = (int)_ambientVolume.Value;
            s.Save();
        }

        private void OnSoundsToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnMusicToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnAmbientToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }

        private void OnSoundsVolumeChanged(double v) { _soundsVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnMusicVolumeChanged(double v) { _musicVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnAmbientVolumeChanged(double v) { _ambientVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }

        private void OnLanguageSelected(long idx)
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var lang = idx switch { 1 => "en", 2 => "zh", _ => "ru-ru" };
            if (s.Language == lang) return;

            s.Language = lang;
            s.Save();
            LocaleLoader.LoadCurrentLanguage();
            RefreshUIText();
            EmitSignal(SignalName.LanguageChanged);
        }

        private void OnBackPressed() => Visible = false;

        private void UpdateSlidersEditable()
        {
            _soundsVolume.Editable = _soundsToggle.ButtonPressed;
            _musicVolume.Editable = _musicToggle.ButtonPressed;
            _ambientVolume.Editable = _ambientToggle.ButtonPressed;
        }
    }
}
