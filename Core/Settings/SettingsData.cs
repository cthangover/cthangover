using Godot;

namespace Cthangover.Core.Settings
{
    public class SettingsData
    {
        private const string ConfigPath = "user://settings.cfg";

        public bool   SoundsEnabled   { get; set; }
        public bool   MusicsEnabled   { get; set; }
        public bool   AmbientEnabled  { get; set; }
        public int    SoundsVolume    { get; set; }
        public int    MusicsVolume    { get; set; }
        public int    AmbientVolume   { get; set; }
        public string Language        { get; set; }
        public int    BattleSpeed     { get; set; }
        public int    WindowMode      { get; set; }
        public int    ResolutionWidth  { get; set; }
        public int    ResolutionHeight { get; set; }
        public int    CurrentScreen   { get; set; }
        public bool   VsyncEnabled    { get; set; }
        public int    Scale           { get; set; }
        public bool   LauncherShown   { get; set; }

        public SettingsData()
        {
            ApplyDefaults();
        }

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
