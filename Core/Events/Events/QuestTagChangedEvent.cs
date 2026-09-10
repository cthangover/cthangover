namespace Cthangover.Core.Events
{
    [EventKey("quest_tag_changed")]
    public class QuestTagChangedEvent : ModEvent
    {
        public string QuestId { get; init; }
        public string Tag { get; init; }
        public bool Added { get; init; }
    }
}
