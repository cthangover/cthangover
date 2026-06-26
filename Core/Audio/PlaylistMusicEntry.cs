using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Audio
{
    /// <summary>
    /// A single playlist entry in JSON — pairs a MusicType with the list of
    /// asset names that belong to it. <c>JsonStringEnumConverter</c> is
    /// used so the type is serialised as a string rather than an integer.
    /// </summary>
    public class PlaylistMusicEntry
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MusicType MusicType { get; set; }
        public List<string> MusicNames { get; set; }
    }
}
