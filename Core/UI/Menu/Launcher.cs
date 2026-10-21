using System;
using System.Threading;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Testing;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Cthangover.Core.UI.Base;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Pre-game launcher that lets the player configure display settings
    /// (window mode, monitor, window size, V-Sync) on the first start before
    /// entering the main menu; later starts skip it and only restore the saved
    /// settings. The choices and the window handling are shared with the
    /// settings menu (<see cref="DisplayOptions"/>, <see cref="DisplaySettingsService"/>).
    ///
    /// Built entirely in code (no TSCN layout) so the scene file is minimal
    /// and settings population is co-located with the logic.
    /// </summary>
    public partial class Launcher : Control
    {
        private OptionButton _windowModeSelect;
        private OptionButton _resolutionSelect;
        private OptionButton _monitorSelect;
        private CheckButton _vsyncToggle;
        private System.Collections.Generic.List<Vector2I> _windowSizes = new();

        /// <summary>
        /// Cancels the background mod compile if the launcher leaves the tree
        /// (player quits while the boot overlay is still streaming the log).
        /// The token only prevents the pipeline from reaching
        /// <see cref="ModManager.FinalizeGodotInit"/> on a dying scene; the
        /// running Roslyn emit still completes, see
        /// <see cref="ModManager.DiscoverAndCompile"/>.
        /// </summary>
        private readonly CancellationTokenSource _bootCts = new();

        /// <summary>Applies the theme, then branches into boot overlay, skip-to-main-menu or the interactive launcher depending on boot state.</summary>
        public override void _Ready()
        {
            CthangoverTheme.Apply(this);

            if (BootController.IsLauncherBoot)
            {
                ShowBootOverlay();
                CallDeferred(nameof(RunBootAsync));
                return;
            }

            if (GameData.Instance?.Settings?.LauncherShown == true)
            {
                CallDeferred(nameof(SkipToMainMenu));
                return;
            }

            BuildUI();
            LoadSettingsToUI();
        }

        /// <summary>
        /// Aborts the launcher-driven boot pipeline when the scene is torn
        /// down, so <see cref="RunBootAsync"/> does not finalize mods or touch
        /// freed UI after the player has already left. Only cancellation is
        /// performed here — disposing the source could race with the pending
        /// boot task reading <c>Token</c>.
        /// </summary>
        public override void _ExitTree()
        {
            _bootCts.Cancel();
        }

        private Control _bootOverlay;

        /// <summary>
        /// Full-screen dark overlay with a streaming boot log and a localized
        /// first-launch hint. Drawn before mod initialisation so the player sees
        /// live progress instead of a frozen window while mods compile on a
        /// background thread.
        /// </summary>
        private void ShowBootOverlay()
        {
            _bootOverlay = new Control();
            _bootOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
            _bootOverlay.MouseFilter = Control.MouseFilterEnum.Stop;

            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = UiTokens.Page;
            _bootOverlay.AddChild(bg);

            var hint = new Label();
            hint.Text = BootMessages.FirstLaunchHint(GameData.Instance?.Settings?.Language);
            hint.SetAnchorsPreset(LayoutPreset.TopWide);
            hint.OffsetLeft = UiTokens.Space8;
            hint.OffsetRight = -UiTokens.Space8;
            hint.OffsetTop = UiTokens.Space9;
            hint.OffsetBottom = UiTokens.Space9;
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.AddThemeFontSizeOverride("font_size", UiTokens.FontLead);
            hint.AddThemeColorOverride("font_color", UiTokens.Body);
            _bootOverlay.AddChild(hint);

            // Boot progress bar: polls the thread-safe BootProgress snapshot in
            // _Process instead of subscribing to worker-thread events, so no
            // Godot API is ever touched off the main thread.
            var progress = new BootProgressBar();
            progress.SetAnchorsPreset(LayoutPreset.TopWide);
            progress.OffsetLeft = UiTokens.Space8;
            progress.OffsetRight = -UiTokens.Space8;
            progress.OffsetTop = UiTokens.Space9 + 48;
            progress.OffsetBottom = progress.OffsetTop + 10;
            _bootOverlay.AddChild(progress);

            var log = new BootLogPanel();
            log.SetAnchorsPreset(LayoutPreset.FullRect);
            _bootOverlay.AddChild(log);

            AddChild(_bootOverlay);
        }

        private void HideBootOverlay()
        {
            if (_bootOverlay != null && GodotObject.IsInstanceValid(_bootOverlay))
                _bootOverlay.QueueFree();
            _bootOverlay = null;
        }

        /// <summary>
        /// Thin UI wrapper over <see cref="Boot.RunLauncherAsync"/>: the unified
        /// boot pipeline yields a frame so the boot overlay renders first, then
        /// resolves the Godot-bound paths, runs mod discovery + compilation on a
        /// background thread and finishes the Godot-bound phases (finalize,
        /// runtime, locale, scene registries) on the main thread. After the
        /// pipeline completes, reloads the mod-provided background sprites, hides
        /// the boot overlay and finishes either with the settings UI or a direct
        /// skip to the main menu.
        /// </summary>
        private async void RunBootAsync()
        {
            try
            {
                await Boot.RunLauncherAsync(this, _bootCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed boot must never soft-lock the launcher behind the
                // boot overlay: log loudly, drop the overlay and keep going so
                // the window stays responsive; readiness guards in ModRegistry
                // keep every too-early mod consumer loud until a re-run.
                GameLogger.Log("BOOT", $"Launcher boot FAILED: {ex.Message}", LogLevel.Error);
            }

            if (!GodotObject.IsInstanceValid(this))
                return;

            // Mod unit-test gate (plan-mod-testing.md §3.1/§5.1): after a fully
            // completed boot the engine — and only on an explicit request —
            // compiles and runs mod tests instead of entering the main menu,
            // then quits with the standard exit code contract.
            if (Boot.IsReady && ModTestEngine.IsTestMode())
            {
                var requestedWhileTesting = SceneLaunchArgs.RequestedSceneName();
                if (requestedWhileTesting != null)
                    GameLogger.Log("SCENE", $"--scene='{requestedWhileTesting}' ignored: --run-tests takes priority", LogLevel.Warning);
                await RunModTestsAndQuitAsync();
                return;
            }

            // Direct scene navigation (--scene=<logical>): a first-class launch
            // mode after a fully completed boot. Skips the launcher UI and the
            // main menu, drops the player into the requested mod-defined scene
            // through the same base-scene + deferred SwitchScene sequence every
            // logical navigation uses. An unknown name logs an error and falls
            // through to the normal launcher flow instead of crashing.
            if (Boot.IsReady && SceneLaunchArgs.RequestedSceneName() is { } sceneTarget)
            {
                await LaunchRequestedSceneAsync(sceneTarget);
                return;
            }

            // Game launch (--game=<id> [--new | --load=<save>]): the boot already
            // kept only that game's mods, so go straight into it. A failed load
            // falls through to the normal launcher flow with the error logged.
            if (Boot.IsReady && Games.ActiveGame.IsActive && EnterActiveGame())
                return;

            GetNodeOrNull<ModSpriteLoader>("Background/SpriteLoader")?.Reload();

            HideBootOverlay();

            if (GameData.Instance?.Settings?.LauncherShown == true)
            {
                SkipToMainMenu();
                return;
            }

            BuildUI();
            LoadSettingsToUI();
        }

        /// <summary>
        /// Feature terminus of <c>--scene=&lt;logical&gt;</c>: validates the name
        /// against <see cref="SceneManager.HasScene"/>, then replays the canonical
        /// logical-scene entry sequence in the core — instantiate the mod-provided
        /// base scene shell (the ViewBox/event host the game expects to live in),
        /// and issue <see cref="SceneManager.SwitchScene"/> on the next frames once
        /// the autoloads have settled. Because only the shell is swapped, all scene
        /// routing (routes, native Godot scenes, scenario scenes) works unchanged.
        /// On an unknown name the error is logged and the launcher falls back to
        /// its normal flow (menu/UI) — a typo must never crash the game.
        /// </summary>
        /// <summary>
        /// Enters the active game from the launch args: loads the requested save
        /// or starts a new game at the collection's entry scene. Returns false
        /// when the requested save could not be loaded.
        /// </summary>
        private bool EnterActiveGame()
        {
            var args = Games.GameLaunchArgs.Current;
            HideBootOverlay();
            if (!string.IsNullOrEmpty(args.LoadSave))
            {
                if (Games.GameSession.LoadAndEnter(this, args.LoadSave))
                    return true;

                GameLogger.Log("GAME", $"Save '{args.LoadSave}' of game '{Games.ActiveGame.Id}' could not be loaded", LogLevel.Error);
                return false;
            }

            Games.GameSession.StartNew(this);
            return true;
        }

        private async System.Threading.Tasks.Task LaunchRequestedSceneAsync(string sceneName)
        {
            var sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
            if (sceneManager == null)
            {
                GameLogger.Log("SCENE", "--scene: SceneManager autoload not found, continuing normal launch", LogLevel.Error);
                return;
            }

            if (!sceneManager.HasScene(sceneName))
            {
                GameLogger.Log("SCENE", $"--scene: logical scene '{sceneName}' not found, continuing normal launch", LogLevel.Error);
                return;
            }

            GameLogger.Log("SCENE", $"--scene: launching logical scene '{sceneName}'");

            var baseScenePath = GodotSceneService.GetScenePath(GodotSceneType.BaseScene);
            var baseScene = TscnScenes.LoadAndBuild(baseScenePath);
            if (baseScene == null)
            {
                GameLogger.Log("SCENE", $"--scene: failed to build base scene '{baseScenePath}', continuing normal launch", LogLevel.Error);
                return;
            }

            GetTree().Root.AddChild(baseScene);

            // Let the shell enter the tree (its _Ready wires the event chain and
            // ViewBox) before the scene pipeline starts publishing events into it.
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

            if (!GodotObject.IsInstanceValid(this))
                return;

            sceneManager.SwitchScene(sceneName);

            // The launcher scene is no longer the front: free its UI so the
            // base scene shell is the only thing on screen.
            GetTree().CurrentScene = baseScene;
            QueueFree();
        }

        /// <summary>
        /// Test-mode terminus of the boot pipeline: runs the mod test engine
        /// for all eligible mods (or the <c>--mod=</c> filtered one), prints
        /// the report through the standard channels and quits the process with
        /// exit code 0/1 — the same contract every integration driver uses.
        /// </summary>
        private async System.Threading.Tasks.Task RunModTestsAndQuitAsync()
        {
            GameLogger.Log("MODTEST", "Test mode active (--run-tests / run_mod_tests): running mod tests");
            // Taken up front: the quit must happen even if this node is gone
            // by the end of the run, otherwise the process hangs until an
            // external timeout kills it.
            var tree = GetTree();
            bool success;
            try
            {
                success = await ModTestEngine.RunAllAsync(ModTestEngine.RequestedModFilter(), _bootCts.Token);
            }
            catch (OperationCanceledException)
            {
                success = false;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODTEST", $"Mod test run failed: {ex.Message}", LogLevel.Error);
                success = false;
            }
            GameLogger.Log("MODTEST", $"Mod test run finished, success={success}, quitting");
            tree.Quit(success ? 0 : 1);
        }

        private void SkipToMainMenu()
        {
            DisplaySettingsService.Apply(GameData.Instance?.Settings);
            OpenMainMenu();
        }

        private void OpenMainMenu() => Games.GameSession.OpenMainMenu(GetTree());

        private void BuildUI()
        {
            var bg = new ColorRect();
            bg.AnchorsPreset = (int)LayoutPreset.FullRect;
            bg.Color = UiTokens.Page;
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
            title.Uppercase = true;
            title.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
            title.AddThemeColorOverride("font_color", UiTokens.InkBright);
            vbox.AddChild(title);

            vbox.AddChild(new HSeparator());

            var modes = new string[DisplayOptions.WindowModeKeys.Length];
            for (var i = 0; i < modes.Length; i++)
                modes[i] = Tr(DisplayOptions.WindowModeKeys[i]);
            _windowModeSelect = AddDropdownRow(vbox, "settings/window_mode", modes);
            _windowModeSelect.ItemSelected += OnWindowModeSelected;

            _monitorSelect = AddDropdownRow(vbox, "settings/monitor", BuildMonitorLabels());
            _monitorSelect.ItemSelected += OnMonitorSelected;

            _resolutionSelect = AddDropdownRow(vbox, "settings/resolution", System.Array.Empty<string>());

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
            CthangoverTheme.ApplyDanger(exitBtn);
            btnRow.AddChild(exitBtn);

            var playBtn = new Button { Name = "LauncherPlay" };
            playBtn.Text = Tr("launcher/play");
            playBtn.Pressed += OnPlayClick;
            CthangoverTheme.ApplyPrimary(playBtn);
            btnRow.AddChild(playBtn);

            vbox.AddChild(btnRow);
        }

        private static OptionButton AddDropdownRow(VBoxContainer parent, string labelKey, string[] items)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var label = new Label();
            label.Text = TranslationServer.Translate(labelKey);
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
            var labels = new string[DisplaySettingsService.ScreenCount];
            for (var i = 0; i < labels.Length; i++)
            {
                var size = DisplayServer.ScreenGetSize(i);
                var key = i == DisplayServer.GetPrimaryScreen() ? "settings/monitor_screen_primary" : "settings/monitor_screen";
                labels[i] = string.Format(Tr(key), i + 1, size.X, size.Y);
            }
            return labels;
        }

        /// <summary>Fills the window sizes for the chosen monitor, keeping <paramref name="wanted"/> (or the nearest smaller size) selected.</summary>
        private void FillWindowSizes(Vector2I wanted)
        {
            _windowSizes = DisplayOptions.WindowSizes(DisplayServer.ScreenGetSize(_monitorSelect.Selected), wanted);
            _resolutionSelect.Clear();
            foreach (var size in _windowSizes)
                _resolutionSelect.AddItem($"{size.X}×{size.Y}");
            _resolutionSelect.Selected = DisplayOptions.WindowSizeIndex(_windowSizes, wanted);
        }

        private Vector2I SelectedWindowSize()
        {
            var index = _resolutionSelect.Selected;
            return index >= 0 && index < _windowSizes.Count ? _windowSizes[index] : DisplayServer.WindowGetSize();
        }

        private void OnMonitorSelected(long index) => FillWindowSizes(SelectedWindowSize());

        private void OnWindowModeSelected(long index) =>
            _resolutionSelect.Disabled = !DisplayOptions.UsesWindowSize((int)DisplayOptions.WindowModes[(int)index]);

        private void LoadSettingsToUI()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var modeIndex = DisplayOptions.WindowModeIndex(s.WindowMode);
            _windowModeSelect.Selected = modeIndex;
            _monitorSelect.Selected = DisplaySettingsService.ClampScreen(s.CurrentScreen);
            FillWindowSizes(new Vector2I(s.ResolutionWidth, s.ResolutionHeight));
            OnWindowModeSelected(modeIndex);
            _vsyncToggle.ButtonPressed = s.VsyncEnabled;
        }

        private void OnPlayClick()
        {
            ApplyAndSave();
            OpenMainMenu();
        }

        private void OnExitClick()
        {
            GetTree().Quit();
        }

        private void ApplyAndSave()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var size = SelectedWindowSize();
            s.WindowMode = (int)DisplayOptions.WindowModes[Mathf.Max(0, _windowModeSelect.Selected)];
            s.CurrentScreen = _monitorSelect.Selected;
            s.ResolutionWidth = size.X;
            s.ResolutionHeight = size.Y;
            s.VsyncEnabled = _vsyncToggle.ButtonPressed;
            s.LauncherShown = true;
            DisplaySettingsService.Apply(s);
            s.Save();
        }
    }
}
