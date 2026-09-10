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
        /// <summary>
        /// The music category for this group of tracks. Serialised as a
        /// string (e.g. "Combat", "Ambient") via
        /// <c>JsonStringEnumConverter</c> so JSON configs remain
        /// human-readable.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MusicType MusicType { get; set; }

        /// <summary>
        /// Asset names belonging to this <see cref="MusicType"/> group.
        /// These are resolved by <c>MusicFactory</c> at playback time.
        /// </summary>
        public List<string> MusicNames { get; set; }
    }
}
