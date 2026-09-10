namespace Cthangover.Core.Events
{
    [EventKey("recruit_left")]
    public class RecruitLeftEvent : ModEvent
    {
        public string RecruitId { get; init; }
        public string RecruitName { get; init; }
    }
}
