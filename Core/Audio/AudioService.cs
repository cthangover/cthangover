using System;
using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Audio
{
    public partial class AudioService : Node, IAudioService
    {
        private AudioStreamPlayer musicPlayer;
        private AudioStreamPlayer ambientPlayer;
        private readonly Dictionary<SoundType, AudioStreamPlayer> soundPlayers = new();

        private int musicBusIndex;
        private int soundBusIndex;
        private int ambientBusIndex;

        private const string MusicBusName = "Music";
        private const string SoundBusName = "SFX";
        private const string AmbientBusName = "Ambient";

        private bool? lastSoundsEnabled;
        private bool? lastMusicsEnabled;
        private bool? lastAmbientEnabled;

        private Random rnd = new Random();

        public override void _Ready()
        {
            AddToGroup("AudioService");
            SetupBuses();
            SetupMusicPlayer();
            SetupAmbientPlayer();
            ApplySettings();
        }

        public override void _Process(double delta)
        {
            var settings = GameData.Instance?.Settings;
            if (settings == null)
                return;

            if (settings.SoundsEnabled != lastSoundsEnabled ||
                settings.SoundsVolume != lastSoundsVolume ||
                settings.MusicsEnabled != lastMusicsEnabled ||
                settings.MusicsVolume != lastMusicsVolume ||
                settings.AmbientEnabled != lastAmbientEnabled ||
                settings.AmbientVolume != lastAmbientVolume)
            {
                ApplySettings();
            }
        }

        private int lastSoundsVolume = -1;
        private int lastMusicsVolume = -1;
        private int lastAmbientVolume = -1;

        private void ApplySettings()
        {
            var settings = GameData.Instance?.Settings;
            if (settings == null)
                return;
            
            GameLogger.Log("AUDIO", $"ApplySettings sounds={settings.SoundsEnabled}/{settings.SoundsVolume} musics={settings.MusicsEnabled}/{settings.MusicsVolume} ambient={settings.AmbientEnabled}/{settings.AmbientVolume}");

            lastSoundsEnabled = settings.SoundsEnabled;
            lastMusicsEnabled = settings.MusicsEnabled;
            lastAmbientEnabled = settings.AmbientEnabled;
            lastSoundsVolume = settings.SoundsVolume;
            lastMusicsVolume = settings.MusicsVolume;
            lastAmbientVolume = settings.AmbientVolume;

            float soundVol = settings.SoundsEnabled ? settings.SoundsVolume : 0;
            float musicVol = settings.MusicsEnabled ? settings.MusicsVolume : 0;
            float ambientVol = settings.AmbientEnabled ? settings.AmbientVolume : 0;
            AudioServer.SetBusVolumeDb(soundBusIndex, LinearToDb(soundVol / 100f));
            AudioServer.SetBusVolumeDb(musicBusIndex, LinearToDb(musicVol / 100f));
            AudioServer.SetBusVolumeDb(ambientBusIndex, LinearToDb(ambientVol / 100f));
        }

        private void SetupBuses()
        {
            musicBusIndex = AudioServer.GetBusIndex(MusicBusName);
            if (musicBusIndex < 0)
            {
                musicBusIndex = AudioServer.GetBusCount();
                AudioServer.AddBus(musicBusIndex);
                AudioServer.SetBusName(musicBusIndex, MusicBusName);
            }

            soundBusIndex = AudioServer.GetBusIndex(SoundBusName);
            if (soundBusIndex < 0)
            {
                soundBusIndex = AudioServer.GetBusCount();
                AudioServer.AddBus(soundBusIndex);
                AudioServer.SetBusName(soundBusIndex, SoundBusName);
            }

            ambientBusIndex = AudioServer.GetBusIndex(AmbientBusName);
            if (ambientBusIndex < 0)
            {
                ambientBusIndex = AudioServer.GetBusCount();
                AudioServer.AddBus(ambientBusIndex);
                AudioServer.SetBusName(ambientBusIndex, AmbientBusName);
            }
        }

        private void SetupMusicPlayer()
        {
            musicPlayer = new AudioStreamPlayer();
            musicPlayer.Name = "MusicPlayer";
            musicPlayer.Bus = MusicBusName;
            AddChild(musicPlayer);
        }

        private void SetupAmbientPlayer()
        {
            ambientPlayer = new AudioStreamPlayer();
            ambientPlayer.Name = "AmbientPlayer";
            ambientPlayer.Bus = AmbientBusName;
            AddChild(ambientPlayer);
        }

        private AudioStreamPlayer GetOrCreateSoundPlayer(SoundType type)
        {
            if (soundPlayers.TryGetValue(type, out var player) && IsInstanceValid(player))
                return player;

            player = new AudioStreamPlayer();
            player.Name = $"SFX_{type}";
            player.Bus = SoundBusName;
            AddChild(player);
            soundPlayers[type] = player;
            return player;
        }

        public void PlayMusic(string id, MusicType musicType)
        {
            if (musicPlayer == null)
                return;

            var stream = MusicFactory.Instance?.Get(id);
            if (stream == null)
                return;

            GameLogger.Log("AUDIO", $"AudioService.PlayMusic '{id}' type={musicType}");

            musicPlayer.Stream = stream;
            musicPlayer.Play();
        }

        public void StopMusic()
        {
            musicPlayer?.Stop();
        }

        public void PauseMusic(bool pause)
        {
            if (musicPlayer == null)
                return;
            musicPlayer.StreamPaused = pause;
        }
        
        public bool IsMusicPlaying()
        {
            return musicPlayer?.Playing ?? false;
        }

        public void PlayAmbient(string id)
        {
            if (ambientPlayer == null)
                return;

            var stream = MusicFactory.Instance?.Get(id);
            if (stream == null)
            {
                GameLogger.Log("AUDIO", $"AudioService.PlayAmbient: stream not found for '{id}'", LogLevel.Error);
                return;
            }

            if (ambientPlayer.Playing && ambientPlayer.Stream == stream)
                return;

            GameLogger.Log("AUDIO", $"AudioService.PlayAmbient '{id}'");

            ambientPlayer.Stream = stream;
            ambientPlayer.Play();
        }

        public void StopAmbient()
        {
            if (ambientPlayer == null || !ambientPlayer.Playing)
                return;

            GameLogger.Log("AUDIO", "AudioService.StopAmbient");
            
            ambientPlayer.Stop();
        }

        public void PlaySound(string id, int variations, SoundType soundType)
        {
            if (variations < 1)
            {
                GameLogger.Log("AUDIO", $"PlaySound '{id}' type={soundType} with invalid variations={variations}");
                return;
            }

            if (variations > 1)
                id += "_" + (rnd.Next(variations) + 1).ToString();
            
            var stream = SoundFactory.Instance?.Get(id);
            if (stream == null)
                return;

            GameLogger.Log("AUDIO", $"PlaySound '{id}' type={soundType}");

            var player = GetOrCreateSoundPlayer(soundType);
            player.Stream = stream;
            player.Play();
        }
        
        public void PlaySound(string id, SoundType soundType)
        {
            PlaySound(id, 1, soundType);
        }

        public void StopSound(SoundType type)
        {
            if (soundPlayers.TryGetValue(type, out var player) && IsInstanceValid(player))
                player.Stop();
        }

        public void PauseSound(SoundType type)
        {
            if (soundPlayers.TryGetValue(type, out var player) && IsInstanceValid(player))
                player.StreamPaused = true;
        }

        public void SetVolume(MixerType type, float volume)
        {
            GameLogger.Log("AUDIO", $"SetVolume {type} = {volume:F2}");

            float db = LinearToDb(volume);
            if (type == MixerType.Musics)
                AudioServer.SetBusVolumeDb(musicBusIndex, db);
            else if (type == MixerType.Ambient)
                AudioServer.SetBusVolumeDb(ambientBusIndex, db);
            else
                AudioServer.SetBusVolumeDb(soundBusIndex, db);
        }

        public void SetEnabled(MixerType type, bool enabled)
        {
            if (enabled)
                return;

            float db = -80f;
            if (type == MixerType.Musics)
                AudioServer.SetBusVolumeDb(musicBusIndex, db);
            else if (type == MixerType.Ambient)
                AudioServer.SetBusVolumeDb(ambientBusIndex, db);
            else
                AudioServer.SetBusVolumeDb(soundBusIndex, db);
        }

        public float GetExpLevel(MixerType type, float percent = 1f)
        {
            var level = GetLevel(type) * percent;
            if (level > 100) level = 100;
            if (level <= 0) level = 0.0001f;
            return Mathf.Log(level / 100f) / Mathf.Log(10) * 20f;
        }

        public float GetLowLevel(MixerType type)
        {
            return GetLevel(type) / 100f;
        }

        private float GetLevel(MixerType type)
        {
            var settings = GameData.Instance?.Settings;
            if (settings == null)
                return 100;

            return type switch
            {
                MixerType.Sounds => settings.SoundsEnabled ? settings.SoundsVolume : 0,
                MixerType.Musics => settings.MusicsEnabled ? settings.MusicsVolume : 0,
                MixerType.Ambient => settings.AmbientEnabled ? settings.AmbientVolume : 0,
                _ => 100,
            };
        }

        public static float LinearToDb(float linear)
        {
            if (linear <= 0.0001f)
                return -80f;
            return Mathf.Log(linear) / Mathf.Log(10) * 20f;
        }
    }
}
