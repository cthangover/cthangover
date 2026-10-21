using System;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Settings.Configs
{
    /// <summary>
    /// Serialisable display defaults read from game_config.json.
    /// WindowMode uses Godot's DisplayServer.WindowMode enum values:
    /// 0=Windowed, 1=Minimized, 2=Maximized, 3=Fullscreen, 4=ExclusiveFullscreen.
    /// The resolution is the window size in windowed mode; fullscreen modes
    /// always run at the monitor's resolution and the canvas_items stretch
    /// scales the 1920×1024 base viewport into the window.
    /// </summary>
    [Serializable]
    public class DisplaySection
    {
        /// <summary>Godot <c>DisplayServer.WindowMode</c> value (default 3 = Fullscreen).</summary>
        [JsonPropertyName("window_mode")]
        public int WindowMode { get; set; } = 3;

        /// <summary>Horizontal resolution in pixels (default 1920).</summary>
        [JsonPropertyName("resolution_width")]
        public int ResolutionWidth { get; set; } = 1920;

        /// <summary>Vertical resolution in pixels (default 1080).</summary>
        [JsonPropertyName("resolution_height")]
        public int ResolutionHeight { get; set; } = 1080;

        /// <summary>Zero-based index of the monitor to use (default 0).</summary>
        [JsonPropertyName("current_screen")]
        public int CurrentScreen { get; set; } = 0;

        /// <summary>Toggles vertical synchronisation (default off).</summary>
        [JsonPropertyName("vsync_enabled")]
        public bool VsyncEnabled { get; set; } = false;

        /// <summary>Frame rate cap, 0 = unlimited (default 0).</summary>
        [JsonPropertyName("max_fps")]
        public int MaxFps { get; set; } = 0;
    }
}
