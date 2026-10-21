namespace Cthangover.Core.Settings
{
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Puts the saved display settings (<see cref="SettingsData"/>) on the game
    /// window: monitor, window mode, window size, V-Sync and the frame rate cap.
    /// The launcher calls <see cref="Apply"/> at start, the settings menu calls
    /// <see cref="ApplyWindow"/> / <see cref="ApplyRendering"/> on every change,
    /// so what the player sees is always what the next start restores.
    /// <para/>
    /// Moving a fullscreen window to another monitor is unreliable on some
    /// platforms, so the window leaves fullscreen first, moves, and enters the
    /// mode again on the new monitor. A windowed window is sized to fit the
    /// monitor's usable area and centred on it — otherwise a size picked on a
    /// big monitor opens half off-screen on a small one. Nothing is touched
    /// when the window is already in the requested state, so changing V-Sync
    /// does not flicker a fullscreen window.
    /// </summary>
    public static class DisplaySettingsService
    {
        /// <summary>Number of monitors, at least 1.</summary>
        public static int ScreenCount => Mathf.Max(1, DisplayServer.GetScreenCount());

        /// <summary><paramref name="screen"/> when such a monitor exists, the primary monitor otherwise (a monitor was unplugged).</summary>
        public static int ClampScreen(int screen) =>
            screen >= 0 && screen < ScreenCount ? screen : DisplayServer.GetPrimaryScreen();

        /// <summary>Applies everything: window first, then V-Sync and the frame rate cap.</summary>
        public static void Apply(SettingsData settings)
        {
            if (settings == null)
                return;
            ApplyWindow(settings);
            ApplyRendering(settings);
        }

        /// <summary>Monitor, window mode and (in windowed mode) the centred window size.</summary>
        public static void ApplyWindow(SettingsData settings)
        {
            var screen = ClampScreen(settings.CurrentScreen);
            var mode = DisplayOptions.WindowModes[DisplayOptions.WindowModeIndex(settings.WindowMode)];

            if (DisplayServer.WindowGetCurrentScreen() != screen)
            {
                if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetCurrentScreen(screen);
            }

            if (mode == DisplayServer.WindowMode.Windowed)
            {
                if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                var area = DisplayServer.ScreenGetUsableRect(screen);
                var size = DisplayOptions.Fit(new Vector2I(settings.ResolutionWidth, settings.ResolutionHeight), area.Size);
                DisplayServer.WindowSetSize(size);
                DisplayServer.WindowSetPosition(area.Position + (area.Size - size) / 2);
            }
            else if (DisplayServer.WindowGetMode() != mode)
            {
                DisplayServer.WindowSetMode(mode);
            }

            GameLogger.Log("UI", $"display settings: screen {screen}, mode {mode}, window {DisplayServer.WindowGetSize()}");
        }

        /// <summary>V-Sync and the frame rate cap; no window changes.</summary>
        public static void ApplyRendering(SettingsData settings)
        {
            DisplayServer.WindowSetVsyncMode(settings.VsyncEnabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = Mathf.Max(0, settings.MaxFps);
        }
    }
}
