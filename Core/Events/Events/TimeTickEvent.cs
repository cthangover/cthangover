namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus on every in-game clock tick; drives anything time-based in mods (day/night logic, timed quests).</summary>
    [EventKey("time_tick")]
    public class TimeTickEvent : ModEvent
    {
        /// <summary>Absolute in-game tick counter.</summary>
        public long TimeTick { get; init; }
        /// <summary>Time of day normalized to 0.0–1.0 across the 24-hour cycle.</summary>
        public float NormalizedTime { get; init; }
    }
}
