using System.Collections.Generic;

namespace Cthangover.Core.Audio
{

    /// <summary>
    /// Runtime playlist model: a scene name mapped to lists of track names
    /// grouped by MusicType. Built by PlaylistFactory from the flatter
    /// <see cref="PlaylistData"/> JSON format.
    /// </summary>
    public class Playlist
    {
        /// <summary>
        /// The scene name this playlist belongs to (e.g. "Tavern", "Battle").
        /// Used as the lookup key in <c>PlaylistContext</c> and
        /// <c>MusicPlayerBehaviour</c> to avoid redundant factory calls
        /// when the scene hasn't changed.
        /// </summary>
        public string                              Scene  { get; set; }

        /// <summary>
        /// Tracks grouped by <see cref="MusicType"/>. The dictionary
        /// provides O(1) access to the track list for the current music
        /// type during auto-advance. An inner <c>List&lt;string&gt;</c>
        /// is used (rather than a set) so duplicate entries are
        /// preserved and random selection is uniform.
        /// </summary>
        public IDictionary<MusicType, List<string>> Musics { get; set; }
    }

}
