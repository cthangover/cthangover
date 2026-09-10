namespace Cthangover.Core.Events
{
    [EventKey("recruit_joined")]
    public class RecruitJoinedEvent : ModEvent
    {
        public string RecruitId { get; init; }
        public string RecruitName { get; init; }
    }
}
