using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Assembles <c>Playlist</c> objects from JSON files under
    /// <c>music/playlists/</c> across all loaded mods. Each playlist maps
    /// music tracks to a scene name and categorises them by
    /// <c>MusicType</c> (ambient, combat, tension, etc.). If a scene has no
    /// dedicated playlist the factory falls back to <c>"default"</c>,
    /// guaranteeing that music never cuts to silence when entering an
    /// unconfigured scene.
    ///
    /// When several mods define playlists for the same scene, a later-loaded
    /// mod <b>replaces</b> the whole track list of a matching
    /// <see cref="MusicType"/> bucket (last one wins) — per-type buckets are
    /// assigned, not concatenated, so a mod can override stock scene music
    /// without editing the base game files.
    /// <para/>
    /// The single instance is owned by this assembly as a lazily created
    /// singleton, exposed through the static <see cref="Instance"/> facade.
    /// </summary>
    public class PlaylistFactory
    {
        private static readonly Lazy<PlaylistFactory> instance = new(() => new PlaylistFactory());

        /// <summary>The lazily created shared instance.</summary>
        public static PlaylistFactory Instance => instance.Value;

        private const string PlaylistsSubDir = "music/playlists";

        /// <summary>Enum overload delegating to <see cref="CreatePlaylist(string)"/> with the scene name.</summary>
        public Playlist CreatePlaylist(GodotSceneType sceneName)
        {
            return CreatePlaylist(sceneName.ToString());
        }

        /// <summary>
        /// Builds the playlist for a scene from every mod's
        /// <c>music/playlists/*.json</c>, falling back to the <c>"default"</c>
        /// playlist when the scene has none (or it is empty).
        /// </summary>
        public Playlist CreatePlaylist(string sceneName)
        {
            var playlist = CreatePlaylistInternal(sceneName);
            if (playlist.Musics.Count > 0)
                return playlist;

            if (string.Equals(sceneName, "default", StringComparison.OrdinalIgnoreCase))
                return playlist;

            GameLogger.Log("AUDIO", $"No playlist for scene '{sceneName}', falling back to 'default'");
            return CreatePlaylistInternal("default");
        }

        private Playlist CreatePlaylistInternal(string sceneName)
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
                        var data = JsonSerializer.Deserialize<PlaylistData>(jsonText, JsonOptionsProvider.ReadOptions);
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
