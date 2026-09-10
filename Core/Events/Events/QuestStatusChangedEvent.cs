namespace Cthangover.Core.Events
{
    [EventKey("quest_status_changed")]
    public class QuestStatusChangedEvent : ModEvent
    {
        public string QuestId { get; init; }
        public string OldStatus { get; init; }
        public string NewStatus { get; init; }
    }
}
