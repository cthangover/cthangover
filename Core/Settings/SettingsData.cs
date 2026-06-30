using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// User-facing settings that persist across sessions via Godot's
    /// <c>ConfigFile</c> API (INI-like format at <c>user://settings.cfg</c>).
    /// Covers audio toggles/volumes, language, battle speed, display mode
    /// (window mode, resolution, monitor, vsync, scale), and launcher state.
    /// On construction, defaults are pulled from <see cref="GameConfig.Instance"/>
    /// so the static JSON config acts as the baseline; <see cref="Load"/>
    /// overwrites these with the user's saved overrides, and <see cref="Save"/>
    /// flushes the current values back to disk.
    /// </summary>
    public class SettingsData
    {
        private const string ConfigPath = "user://settings.cfg";

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
        /// <summary>Locale code string (e.g. "ru-ru").</summary>
        public string Language        { get; set; }
        /// <summary>Battle animation speed multiplier (1 = normal).</summary>
        public int    BattleSpeed     { get; set; }
        /// <summary>Godot <c>DisplayServer.WindowMode</c> value.</summary>
        public int    WindowMode      { get; set; }
        /// <summary>Horizontal resolution override (pixels).</summary>
        public int    ResolutionWidth  { get; set; }
        /// <summary>Vertical resolution override (pixels).</summary>
        public int    ResolutionHeight { get; set; }
        /// <summary>Zero-based monitor index.</summary>
        public int    CurrentScreen   { get; set; }
        /// <summary>Vertical synchronization toggle.</summary>
        public bool   VsyncEnabled    { get; set; }
        /// <summary>Viewport stretch multiplier.</summary>
        public int    Scale           { get; set; }
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
            Scale           = cfg.Display.Scale;
        }

        /// <summary>
        /// Reads <c>user://settings.cfg</c> via Godot's <c>ConfigFile</c> API.
        /// Each value is read with a fallback default (the current property
        /// value), so missing keys leave the in-memory defaults intact.
        /// If the config file does not exist (<c>Error.Ok</c> is not returned),
        /// the method returns silently without changing anything.
        /// </summary>
        public void Load()
        {
            var config = new ConfigFile();
            if (config.Load(ConfigPath) != Error.Ok)
                return;

            SoundsEnabled = (bool)config.GetValue("audio", "sounds_enabled", SoundsEnabled);
            MusicsEnabled = (bool)config.GetValue("audio", "musics_enabled", MusicsEnabled);
            AmbientEnabled = (bool)config.GetValue("audio", "ambient_enabled", AmbientEnabled);
            SoundsVolume  = (int)config.GetValue("audio", "sounds_volume", SoundsVolume);
            MusicsVolume  = (int)config.GetValue("audio", "musics_volume", MusicsVolume);
            AmbientVolume = (int)config.GetValue("audio", "ambient_volume", AmbientVolume);
            Language        = (string)config.GetValue("language", "lang", Language);
            BattleSpeed     = (int)config.GetValue("battle", "battle_speed", BattleSpeed);
            WindowMode      = (int)config.GetValue("display", "window_mode", WindowMode);
            ResolutionWidth  = (int)config.GetValue("display", "resolution_width", ResolutionWidth);
            ResolutionHeight = (int)config.GetValue("display", "resolution_height", ResolutionHeight);
            CurrentScreen   = (int)config.GetValue("display", "current_screen", CurrentScreen);
            VsyncEnabled    = (bool)config.GetValue("display", "vsync_enabled", VsyncEnabled);
            Scale           = (int)config.GetValue("display", "scale", Scale);
            LauncherShown   = (bool)config.GetValue("launcher", "shown", LauncherShown);
        }

        /// <summary>
        /// Writes all current property values to <c>user://settings.cfg</c>
        /// via Godot's <c>ConfigFile</c> API. The file is atomically
        /// replaced (truncated and rewritten) on each call.
        /// </summary>
        public void Save()
        {
            var config = new ConfigFile();
            config.SetValue("audio", "sounds_enabled", SoundsEnabled);
            config.SetValue("audio", "musics_enabled", MusicsEnabled);
            config.SetValue("audio", "ambient_enabled", AmbientEnabled);
            config.SetValue("audio", "sounds_volume", SoundsVolume);
            config.SetValue("audio", "musics_volume", MusicsVolume);
            config.SetValue("audio", "ambient_volume", AmbientVolume);
            config.SetValue("language", "lang", Language);
            config.SetValue("battle", "battle_speed", BattleSpeed);
            config.SetValue("display", "window_mode", WindowMode);
            config.SetValue("display", "resolution_width", ResolutionWidth);
            config.SetValue("display", "resolution_height", ResolutionHeight);
            config.SetValue("display", "current_screen", CurrentScreen);
            config.SetValue("display", "vsync_enabled", VsyncEnabled);
            config.SetValue("display", "scale", Scale);
            config.SetValue("launcher", "shown", LauncherShown);
            config.Save(ConfigPath);
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
            Scale           = other.Scale;
        }
    }
}
