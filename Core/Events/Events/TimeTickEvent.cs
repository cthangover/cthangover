namespace Cthangover.Core.Events
{
    [EventKey("time_tick")]
    public class TimeTickEvent : ModEvent
    {
        public long TimeTick { get; init; }
        public float NormalizedTime { get; init; }
    }
}
