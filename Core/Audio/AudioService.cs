using System;
using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Audio
{
    /// <summary>
    /// Singleton audio hub that owns three independent buses (Music, SFX, Ambient),
    /// creating them at runtime if the audio setup lacks them. For sound effects,
    /// AudioStreamPlayer nodes are pooled by SoundType so overlapping sounds
    /// within the same category are cut off, while different categories stack.
    /// Settings are polled each frame rather than event-driven, detecting
    /// volume/enabled changes cheaply.
    /// Exposes LinearToDb as a public static helper used elsewhere (e.g. UI sliders).
    /// GetExpLevel / GetLowLevel provide logarithmic vs linear volume scaling
    /// for contexts that need one or the other.
    /// </summary>
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
            ambientPlayer.Finished += OnAmbientFinished;
            AddChild(ambientPlayer);
        }

        private void OnAmbientFinished()
        {
            if (ambientPlayer?.Stream != null)
                ambientPlayer.Play();
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

        /// <summary>
        /// Loads a music stream via <c>MusicFactory</c> and hands it to
        /// the dedicated Music player. If either the player or the
        /// factory result is null the call is silently skipped — no
        /// exception is thrown so that callers can fire-and-forget
        /// without guarding against missing assets.
        /// </summary>
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

        /// <summary>
        /// Stops playback on the Music player. The stream reference is
        /// kept intact so a subsequent <c>Play()</c> would restart the
        /// same track — external logic (e.g. auto-advance) is expected
        /// to replace the stream before the next play.
        /// </summary>
        public void StopMusic()
        {
            musicPlayer?.Stop();
        }

        /// <summary>
        /// Toggles the <c>StreamPaused</c> flag on the Music player.
        /// When <paramref name="pause"/> is <c>true</c> playback is
        /// suspended in-place; when <c>false</c> it resumes from the
        /// exact sample. No stream replacement occurs.
        /// </summary>
        public void PauseMusic(bool pause)
        {
            if (musicPlayer == null)
                return;
            musicPlayer.StreamPaused = pause;
        }
        
        /// <summary>
        /// Returns <c>true</c> when the Music player exists and is
        /// currently producing audio. Used externally (e.g. UI) to
        /// display playback state without coupling to the player
        /// node directly.
        /// </summary>
        public bool IsMusicPlaying()
        {
            return musicPlayer?.Playing ?? false;
        }

        /// <summary>
        /// Resolves an ambient loop from <c>SoundFactory</c> and plays it
        /// on the Ambient player. If the same stream is already playing
        /// the call is a no-op, preventing a disruptive restart glitch.
        /// When the stream ends the player automatically loops via the
        /// <c>Finished</c> signal.
        /// </summary>
        public void PlayAmbient(string id)
        {
            if (ambientPlayer == null)
                return;

            var stream = SoundFactory.Instance?.Get(id);
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

        /// <summary>
        /// Stops the Ambient loop. Only acts when the player exists and
        /// is actively playing; otherwise the call is a no-op. There is
        /// no auto-resume — the next <c>PlayAmbient</c> loads the stream
        /// from scratch.
        /// </summary>
        public void StopAmbient()
        {
            if (ambientPlayer == null || !ambientPlayer.Playing)
                return;

            GameLogger.Log("AUDIO", "AudioService.StopAmbient");
            
            ambientPlayer.Stop();
        }

        /// <summary>
        /// Plays a sound effect from <c>SoundFactory</c> with optional
        /// random variation. If <paramref name="variations"/> is greater
        /// than 1, a suffix <c>"_N"</c> is appended to <paramref name="id"/>
        /// where N is uniformly random in [1, variations]. The result is
        /// played on the per-<paramref name="soundType"/> pool player,
        /// cutting off any previous sound of the same type.
        /// </summary>
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
        
        /// <summary>
        /// Convenience overload that delegates to
        /// <c>PlaySound(id, 1, soundType)</c> — no variation suffix is
        /// appended, so the exact asset name is used.
        /// </summary>
        public void PlaySound(string id, SoundType soundType)
        {
            PlaySound(id, 1, soundType);
        }

        /// <summary>
        /// Stops the pooled player for a specific <see cref="SoundType"/>.
        /// Other sound-type players are not affected, so e.g. stopping
        /// <c>CardEffect</c> won't interrupt an ongoing <c>UI</c> sound.
        /// </summary>
        public void StopSound(SoundType type)
        {
            if (soundPlayers.TryGetValue(type, out var player) && IsInstanceValid(player))
                player.Stop();
        }

        /// <summary>
        /// Pauses the player for the given <paramref name="type"/> by
        /// setting <c>StreamPaused = true</c>. A subsequent
        /// <c>PlaySound</c> call will restart it from the paused
        /// position because the stream is already loaded.
        /// </summary>
        public void PauseSound(SoundType type)
        {
            if (soundPlayers.TryGetValue(type, out var player) && IsInstanceValid(player))
                player.StreamPaused = true;
        }

        /// <summary>
        /// Applies a linear volume (0–1) directly to the Godot audio bus
        /// identified by <paramref name="type"/>. The value is converted
        /// to dB via <c>LinearToDb</c>. Unlike the per-frame settings
        /// poll, this is an immediate one-shot override — useful for
        /// fade tweens and manual volume control outside the settings
        /// system.
        /// </summary>
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

        /// <summary>
        /// Silences the bus by forcing -80 dB when
        /// <paramref name="enabled"/> is <c>false</c>. When <c>true</c>
        /// the method returns immediately without restoring volume —
        /// the next settings poll or <c>SetVolume</c> call is expected
        /// to bring the bus back to its intended level. This asymmetry
        /// exists because the settings system is the primary volume
        /// driver and <c>SetEnabled(false)</c> is a hard mute overlay.
        /// </summary>
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

        /// <summary>
        /// Computes a logarithmic (dB) level for scenarios that need
        /// exponential volume scaling (e.g. UI sliders). First clamps
        /// the current bus level scaled by <paramref name="percent"/>
        /// to (0.0001, 100], then converts to dB via
        /// <c>log10(level/100) * 20</c>. The clamping avoids
        /// <c>-infinity</c> dB at zero.
        /// </summary>
        public float GetExpLevel(MixerType type, float percent = 1f)
        {
            var level = GetLevel(type) * percent;
            if (level > 100) level = 100;
            if (level <= 0) level = 0.0001f;
            return Mathf.Log(level / 100f) / Mathf.Log(10) * 20f;
        }

        /// <summary>
        /// Returns the current bus level as a 0–1 linear factor by
        /// dividing the raw 0–100 settings value by 100. Used where
        /// linear interpolation is more appropriate than dB scaling.
        /// </summary>
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

        /// <summary>
        /// Converts a linear amplitude (0–1) to decibels using the
        /// <c>20 * log10(linear)</c> formula. Values at or below
        /// 0.0001 are clamped to -80 dB to avoid <c>-infinity</c>.
        /// Exposed as <c>public static</c> so external code (UI
        /// sliders, settings panels) can perform the conversion
        /// without holding a reference to the service.
        /// </summary>
        public static float LinearToDb(float linear)
        {
            if (linear <= 0.0001f)
                return -80f;
            return Mathf.Log(linear) / Mathf.Log(10) * 20f;
        }
    }
}
