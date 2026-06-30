using System.Collections.Generic;

namespace Cthangover.Core.Audio
{
    /// <summary>
    /// JSON deserialization shape for playlist config files. Flat: one scene
    /// with a list of <see cref="PlaylistMusicEntry"/> objects.
    /// The factory resolves this into a <see cref="Playlist"/> where
    /// entries are already grouped by MusicType for O(1) lookup.
    /// </summary>
	public class PlaylistData
	{
        /// <summary>
        /// The scene name this config entry targets.
        /// </summary>
        public string Scene { get; set; }

        /// <summary>
        /// Flat list of per-type entries. The factory groups these
        /// into the dictionary form in <see cref="Playlist.Musics"/>
        /// for efficient runtime lookup.
        /// </summary>
        public List<PlaylistMusicEntry> Musics { get; set; }
	}
}
