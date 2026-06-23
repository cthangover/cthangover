using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Audio
{
	public partial class MusicPlayerBehaviour : Node
	{
		private AudioStreamPlayer audioPlayer;
		private readonly PlaylistContext playlistContext = new();

		public bool IsCanAutoPlay { get; set; } = true;

		private Tween fadeTween;
		private bool playlistsInited;
		private double autoAdvanceCooldown;

		public override void _Ready()
		{
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

		public void EnabledAutoPlay()
		{
			GameLogger.Log("AUDIO", "Autoplay enabled");

			IsCanAutoPlay = true;
			StopMusic();

			if (playlistContext.LastMusicName != null && audioPlayer != null)
			{
				audioPlayer.Stream = MusicFactory.Instance.Get(playlistContext.LastMusicName);
				if (playlistContext.LastMusicTime > 0)
					audioPlayer.Play((float)playlistContext.LastMusicTime);
			}
		}

		public void DisabledAutoPlay()
		{
			GameLogger.Log("AUDIO", "Autoplay disabled");

			IsCanAutoPlay = false;

			if (playlistContext.LastMusicName != null && audioPlayer != null && audioPlayer.Playing)
				playlistContext.LastMusicTime = audioPlayer.GetPlaybackPosition();

			StopMusic();
		}

		public void UpdateMusicType(GodotSceneType scene)
		{
			UpdateMusicType(scene.ToString());
		}

		public void UpdateMusicType(string sceneName)
		{
			InitPlaylists(sceneName);

			if (!IsCanAutoPlay)
				return;

			var newType = string.Equals(sceneName, "Battle", System.StringComparison.OrdinalIgnoreCase) ? MusicType.Combat : MusicType.Ambient;
			GameLogger.Log("AUDIO", $"Scene '{sceneName}' -> music type {newType} (was {playlistContext.LastMusicType})");

			if (newType != playlistContext.LastMusicType)
			{
				playlistContext.LastMusicType = newType;
				NextSound();
			}
		}

		public MusicType GetMusicType(GodotSceneType scene)
		{
			return scene == GodotSceneType.Battle ? MusicType.Combat : MusicType.Ambient;
		}

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
				int index = (int)GD.Randi() % list.Count;
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

		public void InitPlaylists(GodotSceneType scene)
		{
			InitPlaylists(scene.ToString());
		}

		public void InitPlaylists(string sceneName)
		{
			if (playlistContext.Playlist != null && playlistContext.Playlist.Scene == sceneName)
				return;

			playlistContext.Playlist = PlaylistFactory.Instance.CreatePlaylist(sceneName);
			playlistContext.LastMusicType = MusicType.Ambient;

			var trackCount = playlistContext.Playlist.Musics?.Count ?? 0;
			GameLogger.Log("AUDIO", $"InitPlaylists scene='{sceneName}' tracks={trackCount} autoplay={IsCanAutoPlay}");

			if (playlistContext.Playlist.Musics == null || playlistContext.Playlist.Musics.Count == 0)
				IsCanAutoPlay = false;
		}

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

		public void PlayMusic(string name, bool isLooped = false)
		{
			PlayMusic(name, MusicFactory.Instance.Get(name), isLooped);
		}

		public void PlayMusic()
		{
			audioPlayer?.Play();
			autoAdvanceCooldown = Time.GetTicksUsec() / 1_000_000.0 + 0.5;
		}

		public void PauseMusic()
		{
			GameLogger.Log("AUDIO", "PauseMusic");

			if (audioPlayer != null)
				audioPlayer.StreamPaused = true;
		}

		public void StopMusic()
		{
			GameLogger.Log("AUDIO", "StopMusic");

			audioPlayer?.Stop();
		}

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
