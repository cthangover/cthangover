using System;
using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Menu;
namespace Cthangover.BaseMenu
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
    public partial class SettingsMenu : Control, ISettingsPanel
    {
        /// <summary>
        /// Raised after the player selects a different language and it has been applied.
        /// Parent menus (<see cref="MainMenu"/>, <see cref="GameMenu"/>) subscribe to
        /// refresh their translated button text.
        /// </summary>
        public event Action LanguageChanged;
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

        private VBoxContainer _modListContainer;
        private Label _selectedModName;
        private Button _resetModSettingsBtn;
        private VBoxContainer _settingsContainer;
        private string _selectedModId;
        private readonly Dictionary<Control, string> _controlKeys = new();

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

            _langSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/GeneralMargin/General/LangBox/LangSelect");

            _soundsToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/SoundsBox/ToggleRow/SoundsToggle");
            _soundsVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/SoundsBox/VolumeRow/SoundsVolume");
            _soundsVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/SoundsBox/VolumeRow/SoundsVolumeValue");

            _musicToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/MusicBox/ToggleRow2/MusicToggle");
            _musicVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/MusicBox/VolumeRow2/MusicVolume");
            _musicVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/MusicBox/VolumeRow2/MusicVolumeValue");

            _ambientToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/AmbientBox/ToggleRow3/AmbientToggle");
            _ambientVolume = GetNode<HSlider>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/AmbientBox/VolumeRow3/AmbientVolume");
            _ambientVolumeValue = GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/AmbientBox/VolumeRow3/AmbientVolumeValue");

            _windowModeSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ModeBox/ModeSelect");
            _resolutionSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ResBox/ResSelect");
            _monitorSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/MonitorBox/MonitorSelect");
            _scaleSelect = GetNode<OptionButton>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ScaleBox/ScaleSelect");
            _vsyncToggle = GetNode<CheckButton>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/VSyncRow/VSyncToggle");

            _backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");
            CthangoverTheme.ApplyIcon(_backBtn);

            _modListContainer = GetNode<VBoxContainer>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/LeftPanel/ModScroll/ModList");
            _selectedModName = GetNode<Label>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/RightPanel/TopBar/SelectedModName");
            _resetModSettingsBtn = GetNode<Button>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/RightPanel/TopBar/ResetBtn");
            CthangoverTheme.ApplyDanger(_resetModSettingsBtn);
            _settingsContainer = GetNode<VBoxContainer>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/RightPanel/SettingsScroll/SettingsContainer");

            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Title"), "menu/settings");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/GeneralMargin/General/LangBox/LangLabel"), "settings/language");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/SoundsBox/ToggleRow/SoundsLabel"), "settings/sounds");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/SoundsBox/VolumeRow/SoundsVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/MusicBox/ToggleRow2/MusicLabel"), "settings/music");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/MusicBox/VolumeRow2/MusicVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/AmbientBox/ToggleRow3/AmbientLabel"), "settings/ambient");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/AudioMargin/Audio/AmbientBox/VolumeRow3/AmbientVolumeLabel"), "settings/volume");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ModeBox/ModeLabel"), "settings/window_mode");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ResBox/ResLabel"), "settings/resolution");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/MonitorBox/MonitorLabel"), "settings/monitor");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/ScaleBox/ScaleLabel"), "settings/scale");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/DisplayMargin/Display/VSyncRow/VSyncLabel"), "settings/vsync");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/LeftPanel/ModsTitle"), "settings/mod_list_title");
            RegisterLabel(GetNode<Label>("Panel/Margin/VBox/Tabs/ModSettingsMargin/ModSettings/RightPanel/SettingsScroll/SettingsContainer/Placeholder"), "settings/mods_no_settings");

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
            _resetModSettingsBtn.Pressed += OnResetModSettings;

            LoadSettingsToUI();
            BuildModList();
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
            {
                if (IsInstanceValid(kv.Key))
                    kv.Key.Text = TranslationServer.Translate(kv.Value);
            }
            _backBtn.Text = TranslationServer.Translate("settings/back");
            _tabs.SetTabTitle(0, TranslationServer.Translate("settings/tab_general"));
            _tabs.SetTabTitle(1, TranslationServer.Translate("settings/tab_audio"));
            _tabs.SetTabTitle(2, TranslationServer.Translate("settings/tab_display"));
            _tabs.SetTabTitle(3, TranslationServer.Translate("settings/tab_mods"));
            _resetModSettingsBtn.Text = TranslationServer.Translate("settings/mods_reset");
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
            LanguageChanged?.Invoke();
        }

        /// <inheritdoc cref="IMenuPanel" />
        public void Open() => Visible = true;

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

        private void BuildModList()
        {
            foreach (var child in _modListContainer.GetChildren())
                child.QueueFree();

            var modsWithSettings = ModSettingsRegistry.GetModsWithSettings();

            if (modsWithSettings.Count == 0)
                return;

            foreach (var modId in modsWithSettings)
            {
                var btn = new Button
                {
                    Text = ModSettingsRegistry.GetDisplayTitle(modId),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                btn.Pressed += () => OnModSelected(modId);
                _modListContainer.AddChild(btn);
            }

            var firstMod = modsWithSettings[0];
            OnModSelected(firstMod);
        }

        private void OnModSelected(string modId)
        {
            _selectedModId = modId;
            _selectedModName.Text = ModSettingsRegistry.GetDisplayTitle(modId);
            BuildModSettingsPanel(modId);
        }

        private void BuildModSettingsPanel(string modId)
        {
            _controlKeys.Clear();

            foreach (var child in _settingsContainer.GetChildren())
                child.QueueFree();

            _settingsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var definitions = ModSettingsRegistry.GetDefinitions(modId);
            if (definitions.Count == 0)
            {
                var placeholder = new Label
                {
                    Text = TranslationServer.Translate("settings/mods_no_settings"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                _settingsContainer.AddChild(placeholder);
                return;
            }

            var currentBlob = ModSettingsRegistry.WriteCurrentValues(modId);

            foreach (var def in definitions)
            {
                var row = new HBoxContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 40),
                };
                row.AddThemeConstantOverride("separation", 12);

                var label = new Label
                {
                    Text = TranslationServer.Translate(def.Name),
                    CustomMinimumSize = new Vector2(180, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                row.AddChild(label);

                if (def.Type == SettingType.Action)
                {
                    AddActionControls(row, modId, def);
                    _settingsContainer.AddChild(row);
                    continue;
                }

                Control control = CreateControlForType(def);
                ApplyControlValue(control, def, currentBlob);
                _controlKeys[control] = def.Key;

                switch (def.Type)
                {
                    case SettingType.Bool:
                        if (control is CheckButton checkbox)
                            checkbox.Toggled += _ => OnModSettingChanged();
                        break;
                    case SettingType.Integer:
                    case SettingType.Float:
                        if (control is SpinBox spin)
                            spin.ValueChanged += _ => OnModSettingChanged();
                        break;
                    case SettingType.String:
                        if (control is LineEdit lineEdit)
                            lineEdit.TextChanged += _ => OnModSettingChanged();
                        break;
                    case SettingType.Enum:
                        if (control is OptionButton option)
                            option.ItemSelected += _ => OnModSettingChanged();
                        break;
                    case SettingType.Slider:
                        if (control is HSlider slider)
                        {
                            slider.ValueChanged += _ =>
                            {
                                var val = (double)slider.GetMeta("step", 1.0).AsDouble();
                                var step = val;
                                var stepped = Mathf.Round(slider.Value / step) * step;
                                slider.Value = stepped;
                                OnModSettingChanged();
                            };
                        }
                        break;
                }

                row.AddChild(control);

                if (def.Type == SettingType.Slider)
                {
                    var valLabel = new Label
                    {
                        Text = FormatSliderValue(currentBlob, def),
                        CustomMinimumSize = new Vector2(48, 0),
                        HorizontalAlignment = HorizontalAlignment.Right,
                    };
                    row.AddChild(valLabel);
                }

                _settingsContainer.AddChild(row);
            }

            var timer = new Timer { OneShot = true, Autostart = true, WaitTime = 0.01 };
            timer.Timeout += () =>
            {
                timer.QueueFree();
                LogModSettingsSizes();
            };
            _settingsContainer.AddChild(timer);
        }

        private void LogModSettingsSizes()
        {
            if (_settingsContainer == null)
                return;

            GameLogger.Log("MODS_SETTINGS_UI", $"SettingsContainer size: {_settingsContainer.Size}", LogLevel.Debug);
            foreach (var child in _settingsContainer.GetChildren())
            {
                if (child is HBoxContainer row)
                {
                    GameLogger.Log("MODS_SETTINGS_UI", $"  Row size: {row.Size}", LogLevel.Debug);
                    foreach (var rowChild in row.GetChildren())
                    {
                        if (rowChild is HSlider slider)
                            GameLogger.Log("MODS_SETTINGS_UI", $"    HSlider size: {slider.Size}, min_size: {slider.CustomMinimumSize}", LogLevel.Debug);
                        else if (rowChild is Control c)
                            GameLogger.Log("MODS_SETTINGS_UI", $"    {rowChild.GetType().Name} size: {c.Size}", LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>
        /// Action setting: a button (with an optional confirmation) that runs
        /// <see cref="ModSettingsRegistry.RunAction"/> and shows the result next
        /// to it. Actions store no value, so they stay out of <c>_controlKeys</c>.
        /// </summary>
        private void AddActionControls(HBoxContainer row, string modId, ModSettingDefinition def)
        {
            var button = new Button { Text = TranslationServer.Translate(def.ActionText ?? def.Name) };
            var result = new Label { VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

            void Run() => result.Text = TranslationServer.Translate(ModSettingsRegistry.RunAction(modId, def.Key) ?? string.Empty);

            button.Pressed += () =>
            {
                if (string.IsNullOrEmpty(def.ConfirmText))
                {
                    Run();
                    return;
                }

                var dialog = new ConfirmationDialog
                {
                    DialogText = TranslationServer.Translate(def.ConfirmText),
                    Exclusive = true,
                };
                dialog.Confirmed += () =>
                {
                    Run();
                    dialog.QueueFree();
                };
                dialog.Canceled += dialog.QueueFree;
                AddChild(dialog);
                dialog.PopupCentered();
            };

            row.AddChild(button);
            row.AddChild(result);
        }

        private static Control CreateControlForType(ModSettingDefinition def)
        {
            switch (def.Type)
            {
                case SettingType.Bool:
                    return new CheckButton();
                case SettingType.Integer:
                    return new SpinBox
                    {
                        AllowGreater = true,
                        AllowLesser = true,
                        Step = 1.0,
                        MinValue = int.MinValue,
                        MaxValue = int.MaxValue,
                    };
                case SettingType.Float:
                    return new SpinBox
                    {
                        AllowGreater = true,
                        AllowLesser = true,
                        Step = 0.1,
                        MinValue = double.MinValue,
                        MaxValue = double.MaxValue,
                    };
                case SettingType.String:
                    return new LineEdit();
                case SettingType.Enum:
                {
                    var opt = new OptionButton();
                    if (def.Options != null)
                    {
                        foreach (var kv in def.Options)
                            opt.AddItem(TranslationServer.Translate(kv.Value));
                    }
                    return opt;
                }
                case SettingType.Slider:
                {
                    var slider = new HSlider
                    {
                        MinValue = def.Min,
                        MaxValue = def.Max,
                        Step = 0.01,
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    };
                    slider.SetMeta("step", Variant.From(def.Step > 0 ? def.Step : 1.0));
                    return slider;
                }
                default:
                    return new HBoxContainer();
            }
        }

        private static void ApplyControlValue(Control control, ModSettingDefinition def, DataBlob blob)
        {
            switch (def.Type)
            {
                case SettingType.Bool:
                    if (control is CheckButton checkbox)
                        checkbox.ButtonPressed = blob.GetString(def.Key, def.DefaultValue ?? "false").ToLowerInvariant() == "true";
                    break;
                case SettingType.Integer:
                    if (control is SpinBox spin)
                    {
                        var defVal = int.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0;
                        spin.Value = blob.GetInt(def.Key, defVal);
                    }
                    break;
                case SettingType.Float:
                    if (control is SpinBox spinFloat)
                    {
                        var defVal = float.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0f;
                        spinFloat.Value = blob.GetFloat(def.Key, defVal);
                    }
                    break;
                case SettingType.String:
                    if (control is LineEdit lineEdit)
                        lineEdit.Text = blob.GetString(def.Key, def.DefaultValue ?? "");
                    break;
                case SettingType.Enum:
                    if (control is OptionButton opt)
                    {
                        var current = blob.GetString(def.Key, def.DefaultValue ?? "");
                        opt.Selected = GetOptionIndex(def, current);
                    }
                    break;
                case SettingType.Slider:
                    if (control is HSlider slider)
                    {
                        var defVal = float.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0f;
                        slider.Value = blob.GetFloat(def.Key, defVal);
                    }
                    break;
            }
        }

        private static string ReadControlValue(Control control, SettingType type, ModSettingDefinition def)
        {
            switch (type)
            {
                case SettingType.Bool:
                    return control is CheckButton checkbox ? checkbox.ButtonPressed.ToString().ToLowerInvariant() : "false";
                case SettingType.Integer:
                    return control is SpinBox spin ? ((int)spin.Value).ToString() : "0";
                case SettingType.Float:
                    return control is SpinBox spinFloat ? spinFloat.Value.ToString("F4") : "0";
                case SettingType.String:
                    return control is LineEdit lineEdit ? lineEdit.Text : "";
                case SettingType.Enum:
                    if (control is OptionButton opt && def.Options != null && opt.Selected >= 0 && opt.Selected < def.Options.Count)
                    {
                        var idx = 0;
                        foreach (var kv in def.Options)
                        {
                            if (idx == opt.Selected)
                                return kv.Key;
                            idx++;
                        }
                    }
                    return def.DefaultValue ?? "";
                case SettingType.Slider:
                    return control is HSlider slider ? slider.Value.ToString("F4") : "0";
                default:
                    return "";
            }
        }

        private static string FormatSliderValue(DataBlob blob, ModSettingDefinition def)
        {
            var value = blob.GetFloat(def.Key, float.TryParse(def.DefaultValue, out var d) ? d : 0f);
            var step = def.Step > 0 ? def.Step : 1f;
            if (step >= 1f)
                return $"{(int)value}";
            return $"{value:F2}";
        }

        private static int GetOptionIndex(ModSettingDefinition def, string key)
        {
            if (def.Options == null)
                return 0;
            var idx = 0;
            foreach (var kv in def.Options)
            {
                if (kv.Key == key)
                    return idx;
                idx++;
            }
            return 0;
        }

        private void OnModSettingChanged()
        {
            if (!_initialized || string.IsNullOrEmpty(_selectedModId))
                return;

            var blob = new DataBlob();
            var definitions = ModSettingsRegistry.GetDefinitions(_selectedModId);

            foreach (var def in definitions)
            {
                var control = FindControlForKey(def.Key);
                if (control == null)
                    continue;
                var value = ReadControlValue(control, def.Type, def);
                blob.SetString(def.Key, value);

                if (def.Type == SettingType.Slider)
                    UpdateSliderValueLabel(control);
            }

            ModSettingsRegistry.SaveModSettings(_selectedModId, blob);
            ModSettingsRegistry.ApplyValues(_selectedModId, blob);
        }

        private Control FindControlForKey(string key)
        {
            foreach (var kv in _controlKeys)
            {
                if (kv.Value == key)
                    return kv.Key;
            }
            return null;
        }

        private void UpdateSliderValueLabel(Control slider)
        {
            if (!(slider is HSlider hslider))
                return;

            var parent = slider.GetParent();
            if (parent == null)
                return;

            var lastChild = parent.GetChild(parent.GetChildCount() - 1);
            if (lastChild is Label valLabel)
            {
                var step = (double)slider.GetMeta("step", 1.0).AsDouble();
                if (step >= 1.0)
                    valLabel.Text = $"{(int)hslider.Value}";
                else
                    valLabel.Text = $"{hslider.Value:F2}";
            }
        }

        private void OnResetModSettings()
        {
            if (string.IsNullOrEmpty(_selectedModId))
                return;

            ModSettingsRegistry.DeleteModSettings(_selectedModId);
            BuildModSettingsPanel(_selectedModId);
        }
    }
}
