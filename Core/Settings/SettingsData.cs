using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// User-adjusted settings (audio, display, language, battle speed, launcher
    /// flag) persisted as indented JSON at <c>user://settings.json</c> — a
    /// separate, per-device file that only supplies overrides on top of the
    /// defaults read from <see cref="GameConfig"/>. Constructing the instance
    /// applies the config defaults; <see cref="Load"/> then layers the saved
    /// user values on top.
    /// </summary>
    public class SettingsData
    {
        private const string ConfigPath = "user://settings.json";

        /// <summary>SFX master toggle.</summary>
        public bool   SoundsEnabled   { get; set; }
        /// <summary>Music master toggle.</summary>
        public bool   MusicsEnabled   { get; set; }
        /// <summary>Ambient audio master toggle.</summary>
        public bool   AmbientEnabled  { get; set; }
        /// <summary>SFX volume 0–100.</summary>
        public int    SoundsVolume    { get; set; }
        /// <summary>Music volume 0–100.</summary>
        public int    MusicsVolume    { get; set; }
        /// <summary>Ambient volume 0–100.</summary>
        public int    AmbientVolume   { get; set; }
        /// <summary>Language code in any form (<c>ru</c>, <c>ru-ru</c>, <c>en-en</c>); read through <see cref="Localization.LanguageCode"/>.</summary>
        public string Language        { get; set; }
        /// <summary>Battle animation speed multiplier (1 = normal).</summary>
        public int    BattleSpeed     { get; set; }
        /// <summary>Godot <c>DisplayServer.WindowMode</c> value; the offered ones are listed in <see cref="DisplayWindowModes"/>.</summary>
        public int    WindowMode      { get; set; }
        /// <summary>Window width in windowed mode (pixels); fullscreen modes use the monitor's own resolution.</summary>
        public int    ResolutionWidth  { get; set; }
        /// <summary>Window height in windowed mode (pixels).</summary>
        public int    ResolutionHeight { get; set; }
        /// <summary>Zero-based monitor index.</summary>
        public int    CurrentScreen   { get; set; }
        /// <summary>Vertical synchronization toggle.</summary>
        public bool   VsyncEnabled    { get; set; }
        /// <summary>Frame rate cap (<c>Engine.MaxFps</c>); 0 = unlimited.</summary>
        public int    MaxFps          { get; set; }
        /// <summary><c>true</c> if the launcher window has been shown at least once,
        /// suppressing it on subsequent launches.</summary>
        public bool   LauncherShown   { get; set; }

        /// <summary>
        /// Initialises all properties from <see cref="GameConfig.Instance"/>
        /// defaults (audio, display, language) and hard-coded fallbacks for
        /// settings that have no JSON counterpart (battle speed = 1).
        /// </summary>
        public SettingsData()
        {
            ApplyDefaults();
        }

        /// <summary>
        /// Resets every property to the factory defaults read from
        /// <see cref="GameConfig.Instance"/>. Called during construction
        /// and can be re-invoked to discard user overrides.
        /// </summary>
        public void ApplyDefaults()
        {
            var cfg = GameConfig.Instance;
            SoundsEnabled = cfg.Audio.SoundsEnabled;
            MusicsEnabled = cfg.Audio.MusicsEnabled;
            AmbientEnabled = cfg.Audio.AmbientEnabled;
            SoundsVolume  = cfg.Audio.SoundsVolume;
            MusicsVolume  = cfg.Audio.MusicsVolume;
            AmbientVolume = cfg.Audio.AmbientVolume;
            Language        = cfg.Language;
            BattleSpeed     = 1;
            WindowMode      = cfg.Display.WindowMode;
            ResolutionWidth  = cfg.Display.ResolutionWidth;
            ResolutionHeight = cfg.Display.ResolutionHeight;
            CurrentScreen   = cfg.Display.CurrentScreen;
            VsyncEnabled    = cfg.Display.VsyncEnabled;
            MaxFps          = cfg.Display.MaxFps;
        }

        /// <summary>Resolves the settings file path for the current environment.</summary>
        private static string ResolvePath()
        {
            if (GameEnvironment.IsRuntime)
                return Godot.ProjectSettings.GlobalizePath(ConfigPath);
            return "settings.json";
        }

        /// <summary>
        /// Reads <c>user://settings.json</c> and copies every saved value over
        /// the in-memory defaults (including <see cref="LauncherShown"/>).
        /// Missing file or malformed JSON is silently ignored, keeping defaults.
        /// </summary>
        public void Load()
        {
            var path = ResolvePath();
            if (!File.Exists(path))
                return;

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<SettingsData>(json,
                    JsonOptionsProvider.ReadOptions);
                if (loaded != null)
                {
                    Set(loaded);
                    LauncherShown = loaded.LauncherShown;
                }
            }
            catch { }
        }

        /// <summary>
        /// Serialises all current property values as indented JSON to
        /// <c>user://settings.json</c>, truncating and rewriting the file on
        /// each call. <see cref="LauncherShown"/> is persisted with the rest.
        /// </summary>
        public void Save()
        {
            var path = ResolvePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this,
                JsonOptionsProvider.WriteIndentedOptions);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Bulk-copies all property values from another
        /// <see cref="SettingsData"/> instance. Used to apply preset
        /// configurations (e.g. from a settings dialog) before saving.
        /// Note: <see cref="LauncherShown"/> is intentionally NOT copied.
        /// </summary>
        public void Set(SettingsData other)
        {
            SoundsEnabled = other.SoundsEnabled;
            MusicsEnabled = other.MusicsEnabled;
            AmbientEnabled = other.AmbientEnabled;
            SoundsVolume  = other.SoundsVolume;
            MusicsVolume  = other.MusicsVolume;
            AmbientVolume = other.AmbientVolume;
            Language        = other.Language;
            BattleSpeed     = other.BattleSpeed;
            WindowMode      = other.WindowMode;
            ResolutionWidth  = other.ResolutionWidth;
            ResolutionHeight = other.ResolutionHeight;
            CurrentScreen   = other.CurrentScreen;
            VsyncEnabled    = other.VsyncEnabled;
            MaxFps          = other.MaxFps;
        }
    }
}
