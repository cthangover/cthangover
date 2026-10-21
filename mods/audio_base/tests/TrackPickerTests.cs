namespace Cthangover.Mods.Audio.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Choosing the next track never fails: it avoids repeating the last one
    /// when anything else is available, and a bucket made of one track (or of
    /// copies of it) still plays instead of falling silent.
    /// </summary>
    public class TrackPickerTests
    {
        [ModTest("music/next_track_avoids_repeat_and_never_gives_up")]
        static void NextTrackAvoidsRepeat(ModTestContext ctx)
        {
            var random = new Random(3);
            var list = new List<string> { "a", "b" };
            for (var i = 0; i < 200; i++)
                ModAssert.Equal("b", TrackPicker.Next(list, "a", random), "the other track");

            ModAssert.Equal("a", TrackPicker.Next(new List<string> { "a", "a", "a" }, "a", random), "copies of one track still play");
            ModAssert.Equal("a", TrackPicker.Next(new List<string> { "a" }, "a", random), "a single track repeats");
            ModAssert.Null(TrackPicker.Next(new List<string>(), "a", random), "an empty bucket gives nothing");
        }
    }
}
