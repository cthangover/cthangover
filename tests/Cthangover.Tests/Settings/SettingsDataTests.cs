using System;
using System.IO;
using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class SettingsDataTests : IDisposable
    {
        private const string TestFile = "settings.json";

        public void Dispose()
        {
            try { if (File.Exists(TestFile)) File.Delete(TestFile); }
            catch { }
        }

        [Fact]
        public void Set_copies_all_properties_except_LauncherShown()
        {
            var s1 = new SettingsData();
            var s2 = new SettingsData();

            s2.SoundsVolume = 75;
            s2.MusicsVolume = 50;
            s2.AmbientVolume = 25;
            s2.Language = "en-us";
            s2.BattleSpeed = 2;
            s2.WindowMode = 1;
            s2.ResolutionWidth = 1280;
            s2.ResolutionHeight = 720;
            s2.CurrentScreen = 1;
            s2.VsyncEnabled = true;
            s2.Scale = 2;

            s1.Set(s2);

            Assert.Equal(75, s1.SoundsVolume);
            Assert.Equal(50, s1.MusicsVolume);
            Assert.Equal(25, s1.AmbientVolume);
            Assert.Equal("en-us", s1.Language);
            Assert.Equal(2, s1.BattleSpeed);
            Assert.Equal(1, s1.WindowMode);
            Assert.Equal(1280, s1.ResolutionWidth);
            Assert.Equal(720, s1.ResolutionHeight);
            Assert.Equal(1, s1.CurrentScreen);
            Assert.True(s1.VsyncEnabled);
            Assert.Equal(2, s1.Scale);
        }

        [Fact]
        public void Set_never_copies_LauncherShown_in_either_direction()
        {
            var s1 = new SettingsData();
            var s2 = new SettingsData();

            s1.LauncherShown = true;
            s2.LauncherShown = false;
            s1.Set(s2);
            Assert.True(s1.LauncherShown);

            s1.LauncherShown = false;
            s2.LauncherShown = true;
            s1.Set(s2);
            Assert.False(s1.LauncherShown);
        }

        [Fact]
        public void Constructor_sets_BattleSpeed_to_1()
        {
            Assert.Equal(1, new SettingsData().BattleSpeed);
        }

        [Fact]
        public void Constructor_sets_LauncherShown_to_false()
        {
            Assert.False(new SettingsData().LauncherShown);
        }

        [Fact]
        public void Save_then_Load_roundtrip_preserves_all_values()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);

            var saved = new SettingsData();
            saved.SoundsEnabled = false;
            saved.MusicsEnabled = true;
            saved.AmbientEnabled = false;
            saved.SoundsVolume = 75;
            saved.MusicsVolume = 30;
            saved.AmbientVolume = 10;
            saved.Language = "fr-fr";
            saved.BattleSpeed = 3;
            saved.WindowMode = 2;
            saved.ResolutionWidth = 1600;
            saved.ResolutionHeight = 900;
            saved.CurrentScreen = 1;
            saved.VsyncEnabled = true;
            saved.Scale = 4;
            saved.LauncherShown = true;

            saved.Save();

            var loaded = new SettingsData();
            loaded.Load();

            Assert.Equal(saved.SoundsEnabled, loaded.SoundsEnabled);
            Assert.Equal(saved.MusicsEnabled, loaded.MusicsEnabled);
            Assert.Equal(saved.AmbientEnabled, loaded.AmbientEnabled);
            Assert.Equal(saved.SoundsVolume, loaded.SoundsVolume);
            Assert.Equal(saved.MusicsVolume, loaded.MusicsVolume);
            Assert.Equal(saved.AmbientVolume, loaded.AmbientVolume);
            Assert.Equal(saved.Language, loaded.Language);
            Assert.Equal(saved.BattleSpeed, loaded.BattleSpeed);
            Assert.Equal(saved.WindowMode, loaded.WindowMode);
            Assert.Equal(saved.ResolutionWidth, loaded.ResolutionWidth);
            Assert.Equal(saved.ResolutionHeight, loaded.ResolutionHeight);
            Assert.Equal(saved.CurrentScreen, loaded.CurrentScreen);
            Assert.Equal(saved.VsyncEnabled, loaded.VsyncEnabled);
            Assert.Equal(saved.Scale, loaded.Scale);
            Assert.Equal(saved.LauncherShown, loaded.LauncherShown);
        }

        [Fact]
        public void Load_missing_file_keeps_constructor_defaults()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);

            var s = new SettingsData();
            var musicBefore = s.MusicsVolume;
            s.Load();

            Assert.Equal(musicBefore, s.MusicsVolume);
        }

        [Fact]
        public void Load_invalid_json_does_not_crash()
        {
            File.WriteAllText(TestFile, "{broken json /////");
            var s = new SettingsData();
            s.Load();
        }

        [Fact]
        public void Save_overwrites_existing_file()
        {
            var s1 = new SettingsData { BattleSpeed = 5 };
            s1.Save();

            var s2 = new SettingsData { BattleSpeed = 8 };
            s2.Save();

            var loaded = new SettingsData();
            loaded.Load();
            Assert.Equal(8, loaded.BattleSpeed);
        }
    }
}
