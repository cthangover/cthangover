using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Audio, language, and display settings panel with tabbed layout. Uses an
    /// _initialized guard to prevent save-triggered callbacks from firing during
    /// UI population (LoadSettingsToUI → widget changes → callbacks → premature
    /// saves). Language changes call LocaleLoader.LoadCurrentLanguage() for
    /// immediate effect, then emit LanguageChanged so parent menus can refresh
    /// their translated button text. Sliders become non-editable when their
    /// toggle is off, enforcing the "disabled means off" semantics visually.
    /// Display changes (window mode, resolution, monitor, V-Sync, scale) are
    /// applied live via DisplayServer and ProjectSettings.
    /// </summary>
    public partial class SettingsMenu : Control
    {
        /// <summary>
        /// Emitted after the player selects a different language and it has been applied.
        /// Parent menus (<see cref="MainMenu"/>, <see cref="GameMenu"/>) subscribe to refresh
        /// their translated button text.
        /// </summary>
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

        private OptionButton _windowModeSelect;
        private OptionButton _resolutionSelect;
        private OptionButton _monitorSelect;
        private OptionButton _scaleSelect;
        private CheckButton _vsyncToggle;

        private readonly Dictionary<Label, string> _labelKeys = new();
        private bool _initialized;

        private static readonly Vector2I[] CommonResolutions =
        {
            new(1280, 720),
            new(1366, 768),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160),
        };

        private static readonly int[] ScaleOptions = { 1, 2, 3, 4 };

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

            _windowModeSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/Display/ModeBox/ModeSelect");
            _resolutionSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/Display/ResBox/ResSelect");
            _monitorSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/Display/MonitorBox/MonitorSelect");
            _scaleSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/Display/ScaleBox/ScaleSelect");
            _vsyncToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/Display/VSyncRow/VSyncToggle");

            _backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");

            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Title"), "menu/settings");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/General/LangBox/LangLabel"), "settings/language");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/ToggleRow/SoundsLabel"), "settings/sounds");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/SoundsBox/VolumeRow/SoundsVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/MusicBox/ToggleRow2/MusicLabel"), "settings/music");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/MusicBox/VolumeRow2/MusicVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/ToggleRow3/AmbientLabel"), "settings/ambient");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Audio/AmbientBox/VolumeRow3/AmbientVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Display/ModeBox/ModeLabel"), "settings/window_mode");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Display/ResBox/ResLabel"), "settings/resolution");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Display/MonitorBox/MonitorLabel"), "settings/monitor");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Display/ScaleBox/ScaleLabel"), "settings/scale");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/Display/VSyncRow/VSyncLabel"), "settings/vsync");

            _soundsToggle.Toggled += OnSoundsToggled;
            _musicToggle.Toggled += OnMusicToggled;
            _ambientToggle.Toggled += OnAmbientToggled;
            _soundsVolume.ValueChanged += OnSoundsVolumeChanged;
            _musicVolume.ValueChanged += OnMusicVolumeChanged;
            _ambientVolume.ValueChanged += OnAmbientVolumeChanged;
            _langSelect.ItemSelected += OnLanguageSelected;
            _backBtn.Pressed += OnBackPressed;
            _backBtn.Text = TranslationServer.Translate("settings/back");

            _windowModeSelect.ItemSelected += OnDisplaySettingChanged;
            _resolutionSelect.ItemSelected += OnDisplaySettingChanged;
            _monitorSelect.ItemSelected += OnDisplaySettingChanged;
            _scaleSelect.ItemSelected += OnDisplaySettingChanged;
            _vsyncToggle.Toggled += OnDisplayToggleChanged;

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
            _tabs.SetTabTitle(2, TranslationServer.Translate("settings/tab_display"));
            PopulateLangOptions();
            PopulateDisplayOptions();
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

            _vsyncToggle.ButtonPressed = s.VsyncEnabled;
            _windowModeSelect.Selected = WindowModeToIndex(s.WindowMode);

            UpdateSlidersEditable();
            PopulateLangOptions();
            PopulateDisplayOptions();
            RefreshUIText();

            SelectCurrentResolution(s);
            SelectCurrentMonitor(s);
            SelectCurrentScale(s);
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

        private void PopulateDisplayOptions()
        {
            var previousWindow = _windowModeSelect.Selected;
            var previousRes = _resolutionSelect.Selected;
            var previousMon = _monitorSelect.Selected;
            var previousScale = _scaleSelect.Selected;

            _windowModeSelect.Clear();
            _windowModeSelect.AddItem(TranslationServer.Translate("settings/window_mode_windowed"));
            _windowModeSelect.AddItem(TranslationServer.Translate("settings/window_mode_borderless"));
            _windowModeSelect.AddItem(TranslationServer.Translate("settings/window_mode_fullscreen"));

            _resolutionSelect.Clear();
            foreach (var r in CommonResolutions)
                _resolutionSelect.AddItem($"{r.X}×{r.Y}");

            _monitorSelect.Clear();
            var count = DisplayServer.GetScreenCount();
            if (count <= 0) count = 1;
            for (var i = 0; i < count; i++)
            {
                var size = DisplayServer.ScreenGetSize(i);
                var label = string.Format(TranslationServer.Translate("settings/monitor_screen"), i + 1, size.X, size.Y);
                _monitorSelect.AddItem(label);
            }

            _scaleSelect.Clear();
            foreach (var sc in ScaleOptions)
                _scaleSelect.AddItem(TranslationServer.Translate($"settings/scale_{sc}x"));

            _windowModeSelect.Selected = Mathf.Clamp(previousWindow, 0, 2);
            _resolutionSelect.Selected = Mathf.Clamp(previousRes, 0, CommonResolutions.Length - 1);
            _monitorSelect.Selected = Mathf.Clamp(previousMon, 0, count - 1);
            _scaleSelect.Selected = Mathf.Clamp(previousScale, 0, ScaleOptions.Length - 1);
        }

        private void SelectCurrentResolution(SettingsData s)
        {
            for (var i = 0; i < CommonResolutions.Length; i++)
            {
                if (CommonResolutions[i].X == s.ResolutionWidth && CommonResolutions[i].Y == s.ResolutionHeight)
                {
                    _resolutionSelect.Selected = i;
                    return;
                }
            }
        }

        private void SelectCurrentMonitor(SettingsData s)
        {
            var count = DisplayServer.GetScreenCount();
            if (s.CurrentScreen >= 0 && s.CurrentScreen < count)
                _monitorSelect.Selected = s.CurrentScreen;
        }

        private void SelectCurrentScale(SettingsData s)
        {
            for (var i = 0; i < ScaleOptions.Length; i++)
            {
                if (ScaleOptions[i] == s.Scale)
                {
                    _scaleSelect.Selected = i;
                    return;
                }
            }
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

        private void ApplyDisplayAndSave()
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var windowMode = IndexToWindowMode(_windowModeSelect.Selected);
            var resIdx = Mathf.Clamp(_resolutionSelect.Selected, 0, CommonResolutions.Length - 1);
            var resolution = CommonResolutions[resIdx];
            var monitorIdx = _monitorSelect.Selected;
            var scaleIdx = Mathf.Clamp(_scaleSelect.Selected, 0, ScaleOptions.Length - 1);
            var scale = ScaleOptions[scaleIdx];
            var vsync = _vsyncToggle.ButtonPressed;

            if (monitorIdx >= 0 && monitorIdx < DisplayServer.GetScreenCount())
                DisplayServer.WindowSetCurrentScreen(monitorIdx);

            DisplayServer.WindowSetMode(windowMode);

            if (windowMode == DisplayServer.WindowMode.Windowed)
                DisplayServer.WindowSetSize(resolution);

            DisplayServer.WindowSetVsyncMode(vsync
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);

            ProjectSettings.SetSetting("display/window/stretch/scale", (float)scale);

            s.WindowMode = (int)windowMode;
            s.ResolutionWidth = resolution.X;
            s.ResolutionHeight = resolution.Y;
            s.CurrentScreen = monitorIdx;
            s.VsyncEnabled = vsync;
            s.Scale = scale;
            s.Save();
        }

        private void OnSoundsToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnMusicToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnAmbientToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }

        private void OnSoundsVolumeChanged(double v) { _soundsVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnMusicVolumeChanged(double v) { _musicVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnAmbientVolumeChanged(double v) { _ambientVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }

        private void OnDisplaySettingChanged(long idx) => ApplyDisplayAndSave();
        private void OnDisplayToggleChanged(bool on) => ApplyDisplayAndSave();

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

        private static int WindowModeToIndex(int mode)
        {
            return mode switch
            {
                3 => 1,
                4 => 2,
                _ => 0,
            };
        }

        private static DisplayServer.WindowMode IndexToWindowMode(int index)
        {
            return index switch
            {
                1 => DisplayServer.WindowMode.Fullscreen,
                2 => DisplayServer.WindowMode.ExclusiveFullscreen,
                _ => DisplayServer.WindowMode.Windowed,
            };
        }
    }
}
