using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Audio
{
    public class PlaylistMusicEntry
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MusicType MusicType { get; set; }
        public List<string> MusicNames { get; set; }
    }
}
