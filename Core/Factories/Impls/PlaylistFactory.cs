using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
	public class PlaylistFactory
	{
		private static readonly Lazy<PlaylistFactory> instance = new(() => new PlaylistFactory());
		public static PlaylistFactory Instance => instance.Value;

		private const string PlaylistsSubDir = "music/playlists";

		public Playlist CreatePlaylist(GodotSceneType sceneName)
		{
			return CreatePlaylist(sceneName.ToString());
		}

		public Playlist CreatePlaylist(string sceneName)
		{
			var playlist = new Playlist();
			playlist.Scene = sceneName;
			playlist.Musics = new Dictionary<MusicType, List<string>>();

			var modManager = ModManager.Instance;
			foreach (var kvp in modManager.Mods)
			{
				var modId = kvp.Key;
				var provider = kvp.Value.FileProvider;

				foreach (var entry in provider.ListFiles(PlaylistsSubDir))
				{
					if (entry.EndsWith("/"))
						continue;
					if (!entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
						continue;

					var jsonText = modManager.ReadResolvedText(modId, entry, provider);
					if (jsonText == null)
						continue;

					try
					{
						var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
						var data = JsonSerializer.Deserialize<PlaylistData>(jsonText, options);
						if (data == null || string.IsNullOrEmpty(data.Scene) || data.Musics == null)
							continue;

						if (data.Scene != sceneName)
							continue;

						GameLogger.Log("FACTORY", $"Playlist for scene '{sceneName}' from mod '{modId}' ({entry})");

						foreach (var musicEntry in data.Musics)
						{
							if (musicEntry.MusicNames == null || musicEntry.MusicNames.Count == 0)
								continue;
							playlist.Musics[musicEntry.MusicType] = new List<string>(musicEntry.MusicNames);
						}
					}
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MODS", $"JSON parse failed for playlist '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
				}
			}

			return playlist;
		}
	}
}
