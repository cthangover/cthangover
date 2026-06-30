using System;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Settings.Configs
{
    /// <summary>
    /// Serialisable display defaults read from game_config.json.
    /// WindowMode uses Godot's DisplayServer.WindowMode enum values:
    /// 0=Windowed, 1=Minimized, 2=Maximized, 3=Fullscreen, 4=ExclusiveFullscreen.
    /// Scale is the integer stretch multiplier applied to the base viewport
    /// resolution (1920×1024) — kept at 1 by default for pixel-art sharpness.
    /// </summary>
    [Serializable]
    public class DisplaySection
    {
        [JsonPropertyName("window_mode")]
        public int WindowMode { get; set; } = 3;

        [JsonPropertyName("resolution_width")]
        public int ResolutionWidth { get; set; } = 1920;

        [JsonPropertyName("resolution_height")]
        public int ResolutionHeight { get; set; } = 1080;

        [JsonPropertyName("current_screen")]
        public int CurrentScreen { get; set; } = 0;

        [JsonPropertyName("vsync_enabled")]
        public bool VsyncEnabled { get; set; } = false;

        [JsonPropertyName("scale")]
        public int Scale { get; set; } = 1;
    }
}
