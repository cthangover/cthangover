using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Audio
{
    /// <summary>
    /// Autonomous music player that drives playlist auto-advance and
    /// scene-aware track switching. It finds the AudioService's MusicPlayer
    /// via the scene tree (not injected), initialises playlists lazily,
    /// and advances to a random track when playback ends.
    /// The core complexity is the Combat ↔ Ambient handoff:
    /// when entering combat, the current ambient track and time are saved;
    /// when leaving combat, they're restored — creating a seamless
    /// interruption model. FadeMusic uses a Tween with a 1s delay
    /// followed by a 6s volume ramp.
    /// Disabling auto-play preserves the last track/time; re-enabling
    /// resumes from the saved point.
    /// </summary>
	public partial class MusicPlayerBehaviour : Node
	{
		private AudioStreamPlayer audioPlayer;
		private readonly PlaylistContext playlistContext = new();

        /// <summary>
        /// Top-level toggle for the entire auto-advance state machine.
        /// When <c>false</c>, <c>NextSound</c> is skipped, no new
        /// tracks are selected, and <c>UpdateMusicType</c> does not
        /// trigger a switch. Set by <c>EnabledAutoPlay</c> /
        /// <c>DisabledAutoPlay</c> and also forced to <c>false</c>
        /// when a playlist has no tracks.
        /// </summary>
        public bool IsCanAutoPlay { get; set; } = true;

		private Tween fadeTween;
		private bool playlistsInited;
		private double autoAdvanceCooldown;

		public override void _Ready()
		{
			AddToGroup("music_player");
			TryFindAudioPlayer();
		}

		public override void _Process(double delta)
		{
			if (audioPlayer == null || !IsInstanceValid(audioPlayer))
			{
				TryFindAudioPlayer();
				return;
			}

			if (!playlistsInited)
			{
			var sceneMgr = GetNodeOrNull<Scenes.SceneManager>("/root/SceneManager");
				var sceneName = sceneMgr?.CurrentSceneName ?? GameData.Instance?.Runtime?.CurrentScene.ToString() ?? "MainMenu";
				InitPlaylists(sceneName);
				playlistsInited = true;
				autoAdvanceCooldown = Time.GetTicksUsec() / 1_000_000.0 + 0.5;
			}

			if (!IsCanAutoPlay)
				return;

			double now = Time.GetTicksUsec() / 1_000_000.0;
			if (now > autoAdvanceCooldown && !audioPlayer.Playing && !audioPlayer.StreamPaused)
			{
				NextSound();
				autoAdvanceCooldown = now + 0.5;
			}
		}

        /// <summary>
        /// Re-enables auto-play after it was disabled. Stops current
        /// playback and, if a <c>LastMusicName</c> is saved, restores
        /// that track from <c>MusicFactory</c> — effectively resuming
        /// the previously-interrupted song. If no track is saved the
        /// auto-advance loop in <c>_Process</c> will pick the next
        /// random track within 0.5s.
        /// </summary>
        public void EnabledAutoPlay()
        {
			GameLogger.Log("AUDIO", "Autoplay enabled");

			IsCanAutoPlay = true;
			StopMusic();

			if (playlistContext.LastMusicName != null && audioPlayer != null)
			{
				audioPlayer.Stream = MusicFactory.Instance.Get(playlistContext.LastMusicName);
				audioPlayer.Play();
			}
		}

        /// <summary>
        /// Disables auto-play and saves the current playback position
        /// into <c>PlaylistContext.LastMusicTime</c> so it can be
        /// restored later via <c>EnabledAutoPlay</c>. Stops playback
        /// immediately after saving.
        /// </summary>
        public void DisabledAutoPlay()
        {
			GameLogger.Log("AUDIO", "Autoplay disabled");

			IsCanAutoPlay = false;

			if (playlistContext.LastMusicName != null && audioPlayer != null && audioPlayer.Playing)
				playlistContext.LastMusicTime = audioPlayer.GetPlaybackPosition();

			StopMusic();
		}

        /// <summary>
        /// Overload that delegates to
        /// <c>UpdateMusicType(scene.ToString())</c>.
        /// </summary>
        public void UpdateMusicType(GodotSceneType scene)
        {
			UpdateMusicType(scene.ToString());
		}

        /// <summary>
        /// Scene-transition entry point that orchestrates the
        /// Combat ↔ Ambient music handoff. Re-initialises the
        /// playlist for the new scene and determines the appropriate
        /// <see cref="MusicType"/>:
        /// <list type="bullet">
        /// <item>If the type is unchanged and the current track exists
        /// in the new playlist, playback continues uninterrupted.</item>
        /// <item>If the type is unchanged but the track is absent from
        /// the new playlist, a new random track is picked.</item>
        /// <item>When transitioning <b>Ambient → Combat</b>: the
        /// current ambient track name and playback position are saved
        /// to <c>PlaylistContext</c> so they can be restored after
        /// combat ends.</item>
        /// <item>When transitioning <b>Combat → Ambient</b>: the
        /// saved ambient state is restored. If the saved time is past
        /// 0.5s, <c>OggPacketParser.CreateTrimmedStream</c> is used to
        /// create a truncated OGG stream starting from the saved
        /// position — this avoids the O(N) seek penalty of
        /// <c>Play(fromPosition)</c> on large OGG files.</item>
        /// </list>
        /// When auto-play is disabled the entire method is a no-op
        /// except for playlist initialisation.
        /// </summary>
        public void UpdateMusicType(string sceneName)
        {
			var previousType = playlistContext.LastMusicType;
			InitPlaylists(sceneName);

			if (!IsCanAutoPlay)
				return;

			var newType = string.Equals(sceneName, "Battle", System.StringComparison.OrdinalIgnoreCase) ? MusicType.Combat : MusicType.Ambient;
			GameLogger.Log("AUDIO", $"Scene '{sceneName}' -> music type {newType} (was {previousType})");

			if (newType == previousType)
			{
				if (!string.IsNullOrEmpty(playlistContext.LastMusicName))
				{
					var dict = playlistContext.Playlist?.Musics;
					if (dict != null && dict.TryGetValue(newType, out var list) && list != null && list.Contains(playlistContext.LastMusicName))
					{
						GameLogger.Log("AUDIO", $"Keeping current track '{playlistContext.LastMusicName}' (found in scene playlist)");
						return;
					}
				}
				GameLogger.Log("AUDIO", $"Current track '{playlistContext.LastMusicName}' not in scene playlist, switching");
				NextSound();
				return;
			}

			if (newType == MusicType.Combat && previousType == MusicType.Ambient)
			{
				if (audioPlayer != null && audioPlayer.Playing)
				{
					playlistContext.SavedAmbientMusicName = playlistContext.LastMusicName;
					playlistContext.SavedAmbientMusicTime = audioPlayer.GetPlaybackPosition();
					GameLogger.Log("AUDIO", $"Saved ambient state: '{playlistContext.SavedAmbientMusicName}' at {playlistContext.SavedAmbientMusicTime:F1}s");
				}
				playlistContext.LastMusicType = newType;
				StopMusic();
				NextSound();
				return;
			}

			if (newType == MusicType.Ambient && previousType == MusicType.Combat)
			{
				StopMusic();
				playlistContext.LastMusicType = newType;

				if (!string.IsNullOrEmpty(playlistContext.SavedAmbientMusicName) && audioPlayer != null)
				{
					var stream = MusicFactory.Instance.Get(playlistContext.SavedAmbientMusicName);
					if (stream != null)
					{
						var savedTime = (double)playlistContext.SavedAmbientMusicTime;
						GameLogger.Log("AUDIO", $"Restoring ambient: '{playlistContext.SavedAmbientMusicName}' was at {savedTime:F1}s");

						if (savedTime > 0.5f)
						{
							var trimmed = OggPacketParser.CreateTrimmedStream(stream, savedTime);
							if (trimmed != null)
							{
								stream = trimmed;
								GameLogger.Log("AUDIO", $"Trimmed stream created — playing from 0 (effective start {savedTime:F1}s)");
							}
						}

						audioPlayer.Stream = stream;
						playlistContext.LastMusicName = playlistContext.SavedAmbientMusicName;
						audioPlayer.Play();
						playlistContext.SavedAmbientMusicName = null;
						playlistContext.SavedAmbientMusicTime = 0;
						autoAdvanceCooldown = Time.GetTicksUsec() / 1_000_000.0 + 0.5;
						return;
					}
					GameLogger.Log("AUDIO", $"Restore failed: stream not found for '{playlistContext.SavedAmbientMusicName}'", LogLevel.Error);
					playlistContext.SavedAmbientMusicName = null;
					playlistContext.SavedAmbientMusicTime = 0;
				}
				else
				{
					GameLogger.Log("AUDIO", "No saved ambient state to restore");
				}
				NextSound();
				return;
			}

			playlistContext.LastMusicType = newType;
			NextSound();
		}

        /// <summary>
        /// Resolves the music type for a scene enum value: returns
        /// <c>MusicType.Combat</c> for <c>GodotSceneType.Battle</c>,
        /// otherwise <c>MusicType.Ambient</c>. Used by external systems
        /// that need to know the music category without entering the
        /// full scene-transition flow.
        /// </summary>
        public MusicType GetMusicType(GodotSceneType scene)
        {
			return scene == GodotSceneType.Battle ? MusicType.Combat : MusicType.Ambient;
		}

        /// <summary>
        /// Picks a random track from the current playlist's
        /// <c>LastMusicType</c> bucket, avoiding the immediately
        /// previous track when the bucket has more than one entry.
        /// Stops the current player and starts the new track via
        /// <c>PlayMusic</c>. Skips silently when auto-play is
        /// disabled or the playlist is empty.
        /// </summary>
        public void NextSound()
        {
			if (!IsCanAutoPlay)
				return;

			var dict = playlistContext.Playlist?.Musics;
			if (dict == null || !dict.TryGetValue(playlistContext.LastMusicType, out var list) || Lists.IsEmpty(list))
				return;

			int iteration = 0;
			for (;;)
			{
				int index = (int)(GD.Randi() % (uint)list.Count);
				var nextMusic = list[index];

				if (list.Count == 1 || playlistContext.LastMusicName != nextMusic)
				{
					GameLogger.Log("AUDIO", $"NextSound -> '{nextMusic}' (type={playlistContext.LastMusicType}, scene={playlistContext.Playlist?.Scene})");
					
					StopMusic();
					PlayMusic(nextMusic);
					break;
				}

				if (iteration++ > 10)
					break;
			}
		}

        /// <summary>
        /// Overload that delegates to
        /// <c>InitPlaylists(scene.ToString())</c>.
        /// </summary>
        public void InitPlaylists(GodotSceneType scene)
        {
			InitPlaylists(scene.ToString());
		}

        /// <summary>
        /// Builds the playlist for a scene via
        /// <c>PlaylistFactory.CreatePlaylist</c>. If the playlist for
        /// the same scene is already loaded the call is a no-op. Forces
        /// <c>LastMusicType</c> from <c>Force</c> to <c>Ambient</c> so
        /// transient forced tracks don't persist across scenes. If the
        /// resulting playlist has no tracks, disables auto-play
        /// entirely.
        /// </summary>
        public void InitPlaylists(string sceneName)
        {
			if (playlistContext.Playlist != null && playlistContext.Playlist.Scene == sceneName)
				return;

			playlistContext.Playlist = PlaylistFactory.Instance.CreatePlaylist(sceneName);

			if (playlistContext.LastMusicType == MusicType.Force)
				playlistContext.LastMusicType = MusicType.Ambient;

			var trackCount = playlistContext.Playlist.Musics?.Count ?? 0;
			GameLogger.Log("AUDIO", $"InitPlaylists scene='{sceneName}' tracks={trackCount} autoplay={IsCanAutoPlay}");

			if (playlistContext.Playlist.Musics == null || playlistContext.Playlist.Musics.Count == 0)
				IsCanAutoPlay = false;
		}

        /// <summary>
        /// Manually plays a specific track with an explicit
        /// <c>AudioStream</c>. Updates <c>LastMusicName</c> and resets
        /// the auto-advance cooldown to prevent an immediate skip.
        /// The <paramref name="isLooped"/> parameter is accepted but
        /// currently unused — OGG streams are expected to be
        /// non-looping and auto-advance handles the next-track
        /// selection.
        /// </summary>
        public void PlayMusic(string name, AudioStream music, bool isLooped = false)
        {
			if (audioPlayer == null || !IsInstanceValid(audioPlayer))
				TryFindAudioPlayer();
			if (audioPlayer == null)
				return;

			GameLogger.Log("AUDIO", $"PlayMusic '{name}'");

			playlistContext.LastMusicName = name;
			audioPlayer.Stream = music;
			audioPlayer.Play();

			autoAdvanceCooldown = Time.GetTicksUsec() / 1_000_000.0 + 0.5;
		}

        /// <summary>
        /// Plays a track by name, resolving the stream from
        /// <c>MusicFactory</c>. Delegates to the
        /// <c>PlayMusic(name, stream, isLooped)</c> overload.
        /// </summary>
        public void PlayMusic(string name, bool isLooped = false)
        {
			PlayMusic(name, MusicFactory.Instance.Get(name), isLooped);
		}

        /// <summary>
        /// Resumes the currently-loaded stream on the audio player.
        /// Unlike the named overloads, does not change the stream
        /// or <c>LastMusicName</c>. Resets the auto-advance cooldown
        /// so the just-resumed track isn't replaced immediately.
        /// </summary>
        public void PlayMusic()
        {
			audioPlayer?.Play();
			autoAdvanceCooldown = Time.GetTicksUsec() / 1_000_000.0 + 0.5;
		}

        /// <summary>
        /// Pauses the music player in-place via <c>StreamPaused</c>.
        /// Unlike <c>StopMusic</c>, the stream stays loaded and
        /// <c>PlayMusic()</c> will resume from the paused sample.
        /// </summary>
        public void PauseMusic()
        {
			GameLogger.Log("AUDIO", "PauseMusic");

			if (audioPlayer != null)
				audioPlayer.StreamPaused = true;
		}

        /// <summary>
        /// Immediately stops playback on the audio player. The stream
        /// reference is preserved; a subsequent <c>PlayMusic()</c>
        /// would restart the same track from the beginning.
        /// <c>LastMusicName</c> is not cleared, so <c>EnabledAutoPlay</c>
        /// can restore it from the factory.
        /// </summary>
        public void StopMusic()
        {
			GameLogger.Log("AUDIO", "StopMusic");

			audioPlayer?.Stop();
		}

        /// <summary>
        /// Fades the music bus volume from 1 to 0 over 6 seconds after
        /// a 1-second delay, then stops playback and resets the bus to
        /// full volume. The fade is implemented via a Godot <c>Tween</c>
        /// that calls <c>AudioService.SetVolume</c> each frame. If a
        /// previous fade is still running it is killed before starting the
        /// new one. The final reset to volume=1 is necessary because the
        /// settings poll would otherwise keep the bus at zero until the
        /// next <c>ApplySettings</c> cycle.
        /// </summary>
        public void FadeMusic()
        {
			GameLogger.Log("AUDIO", "FadeMusic start (1s delay + 6s fade)");

			fadeTween?.Kill();
			fadeTween = CreateTween();
			fadeTween.TweenInterval(1.0);
			fadeTween.TweenMethod(
				Callable.From<float>(vol =>
				{
					var service = GetNodeOrNull<AudioService>("/root/AudioService");
					service?.SetVolume(MixerType.Musics, vol);
				}),
				1f, 0f, 6.0
			);
			fadeTween.TweenCallback(Callable.From(() =>
			{
				StopMusic();
				var service = GetNodeOrNull<AudioService>("/root/AudioService");
				service?.SetVolume(MixerType.Musics, 1f);
			}));
		}

		private void TryFindAudioPlayer()
		{
			var service = GetNodeOrNull<AudioService>("/root/AudioService");
			if (service != null)
			{
				audioPlayer = service.GetNodeOrNull<AudioStreamPlayer>("MusicPlayer");
			}
		}
	}
}
