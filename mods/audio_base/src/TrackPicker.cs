using System;
using System.Collections.Generic;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Picks the next track of a music bucket: a random one different from the
    /// last played when the bucket offers another, otherwise the last one
    /// again. Choosing among the other tracks directly — instead of rerolling
    /// until the pick differs — cannot give up and leave the player silent
    /// (a bucket holding copies of one track exhausted every reroll).
    /// </summary>
    public static class TrackPicker
    {
        /// <summary>The next track of <paramref name="tracks"/>, or null for an empty bucket.</summary>
        public static string Next(IReadOnlyList<string> tracks, string last, Random random)
        {
            if (tracks == null || tracks.Count == 0)
                return null;

            var others = new List<string>();
            foreach (var track in tracks)
            {
                if (track != last)
                    others.Add(track);
            }

            return others.Count > 0 ? others[random.Next(others.Count)] : tracks[0];
        }
    }
}
