using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Pre-game launcher that lets the player configure display settings
    /// (window mode, resolution, monitor, V-Sync, scale) before entering
    /// the main menu. Applies settings via DisplayServer and ProjectSettings,
    /// then transitions to MainMenu.tscn on Play.
    ///
    /// Built entirely in code (no TSCN layout) so the scene file is minimal
    /// and settings population is co-located with the logic.
    /// </summary>
    public partial class Launcher : Control
    {
        private OptionButton _windowModeSelect;
        private OptionButton _resolutionSelect;
        private OptionButton _monitorSelect;
        private OptionButton _scaleSelect;
        private CheckButton _vsyncToggle;

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
            if (GameData.Instance?.Settings?.LauncherShown == true)
            {
                CallDeferred(nameof(SkipToMainMenu));
                return;
            }

            BuildUI();
            LoadSettingsToUI();
        }

        private void SkipToMainMenu()
        {
            var s = GameData.Instance?.Settings;
            if (s != null)
            {
                var mode = (DisplayServer.WindowMode)s.WindowMode;
                DisplayServer.WindowSetMode(mode);
                if (mode == DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetSize(new Vector2I(s.ResolutionWidth, s.ResolutionHeight));
                if (s.CurrentScreen >= 0 && s.CurrentScreen < DisplayServer.GetScreenCount())
                    DisplayServer.WindowSetCurrentScreen(s.CurrentScreen);
                DisplayServer.WindowSetVsyncMode(s.VsyncEnabled
                    ? DisplayServer.VSyncMode.Enabled
                    : DisplayServer.VSyncMode.Disabled);
                ProjectSettings.SetSetting("display/window/stretch/scale", (float)s.Scale);
            }
            GetTree().ChangeSceneToFile("res://scenes/menu/main_menu.tscn");
        }

        private void BuildUI()
        {
            var bg = new ColorRect();
            bg.AnchorsPreset = (int)LayoutPreset.FullRect;
            bg.Color = new Color(0.1f, 0.08f, 0.1f, 1f);
            AddChild(bg);

            var centerPanel = new Panel();
            centerPanel.SetAnchorsPreset(LayoutPreset.Center);
            centerPanel.SetSize(new Vector2(480, 420));
            centerPanel.Position = -centerPanel.Size / 2;
            AddChild(centerPanel);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 24);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_right", 24);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            centerPanel.AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            margin.AddChild(vbox);

            var title = new Label();
            title.Text = Tr("launcher/title");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 28);
            vbox.AddChild(title);

            vbox.AddChild(new HSeparator());

            _windowModeSelect = AddDropdownRow(vbox, "settings/window_mode",
                new[]
                {
                    Tr("settings/window_mode_windowed"),
                    Tr("settings/window_mode_borderless"),
                    Tr("settings/window_mode_fullscreen"),
                });

            _resolutionSelect = AddDropdownRow(vbox, "settings/resolution",
                System.Array.ConvertAll(CommonResolutions, r => $"{r.X}×{r.Y}"));

            _monitorSelect = AddDropdownRow(vbox, "settings/monitor", BuildMonitorLabels());

            _scaleSelect = AddDropdownRow(vbox, "settings/scale",
                System.Array.ConvertAll(ScaleOptions, s => Tr($"settings/scale_{s}x")));

            var vsyncRow = new HBoxContainer();
            vsyncRow.AddThemeConstantOverride("separation", 12);
            var vsyncLabel = new Label();
            vsyncLabel.Text = Tr("settings/vsync");
            vsyncLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vsyncRow.AddChild(vsyncLabel);
            _vsyncToggle = new CheckButton();
            vsyncRow.AddChild(_vsyncToggle);
            vbox.AddChild(vsyncRow);

            vbox.AddChild(new HSeparator());

            var btnRow = new HBoxContainer();
            btnRow.Alignment = BoxContainer.AlignmentMode.End;
            btnRow.AddThemeConstantOverride("separation", 12);

            var exitBtn = new Button();
            exitBtn.Text = Tr("launcher/exit");
            exitBtn.Pressed += OnExitClick;
            btnRow.AddChild(exitBtn);

            var playBtn = new Button();
            playBtn.Text = Tr("launcher/play");
            playBtn.Pressed += OnPlayClick;
            btnRow.AddChild(playBtn);

            vbox.AddChild(btnRow);
        }

        private static OptionButton AddDropdownRow(VBoxContainer parent, string labelKey, string[] items)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var label = new Label();
            label.Text = labelKey;
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(label);

            var dropdown = new OptionButton();
            dropdown.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            foreach (var item in items)
                dropdown.AddItem(item);
            row.AddChild(dropdown);

            parent.AddChild(row);
            return dropdown;
        }

        private string[] BuildMonitorLabels()
        {
            var count = DisplayServer.GetScreenCount();
            if (count <= 0)
                return new[] { "Screen 0" };

            var labels = new string[count];
            for (var i = 0; i < count; i++)
            {
                var size = DisplayServer.ScreenGetSize(i);
                labels[i] = string.Format(Tr("settings/monitor_screen"), i + 1, size.X, size.Y);
            }
            return labels;
        }

        private void LoadSettingsToUI()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            _windowModeSelect.Selected = WindowModeToIndex(s.WindowMode);
            _vsyncToggle.ButtonPressed = s.VsyncEnabled;

            for (var i = 0; i < CommonResolutions.Length; i++)
            {
                if (CommonResolutions[i].X == s.ResolutionWidth && CommonResolutions[i].Y == s.ResolutionHeight)
                {
                    _resolutionSelect.Selected = i;
                    break;
                }
            }

            var screenCount = DisplayServer.GetScreenCount();
            if (s.CurrentScreen >= 0 && s.CurrentScreen < screenCount)
                _monitorSelect.Selected = s.CurrentScreen;

            for (var i = 0; i < ScaleOptions.Length; i++)
            {
                if (ScaleOptions[i] == s.Scale)
                {
                    _scaleSelect.Selected = i;
                    break;
                }
            }
        }

        private void OnPlayClick()
        {
            ApplyAndSave();
            GetTree().ChangeSceneToFile("res://scenes/menu/main_menu.tscn");
        }

        private void OnExitClick()
        {
            GetTree().Quit();
        }

        private void ApplyAndSave()
        {
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
            s.LauncherShown = true;
            s.Save();
        }

        private static int WindowModeToIndex(int mode)
        {
            return mode switch
            {
                3 => 1, // Fullscreen → Borderless
                4 => 2, // ExclusiveFullscreen → Fullscreen
                _ => 0, // Windowed or anything else
            };
        }

        private static DisplayServer.WindowMode IndexToWindowMode(int index)
        {
            return index switch
            {
                1 => DisplayServer.WindowMode.Fullscreen,           // Borderless
                2 => DisplayServer.WindowMode.ExclusiveFullscreen,  // Fullscreen
                _ => DisplayServer.WindowMode.Windowed,             // Windowed
            };
        }
    }
}
