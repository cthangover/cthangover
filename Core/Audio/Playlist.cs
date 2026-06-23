using System.Collections.Generic;

namespace Cthangover.Core.Audio
{

    public class Playlist
    {
        public string                              Scene  { get; set; }
        public IDictionary<MusicType, List<string>> Musics { get; set; }
    }

}
