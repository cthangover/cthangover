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
        public string                              Scene  { get; set; }
        public IDictionary<MusicType, List<string>> Musics { get; set; }
    }

}
