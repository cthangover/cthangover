namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus when a quest transitions between lifecycle states; the payload records both the old and the new status.</summary>
    [EventKey("quest_status_changed")]
    public class QuestStatusChangedEvent : ModEvent
    {
        /// <summary>Id of the quest whose lifecycle state changed.</summary>
        public string QuestId { get; init; }
        /// <summary>Previous QuestStatus enum name.</summary>
        public string OldStatus { get; init; }
        /// <summary>New QuestStatus enum name as written by the caller (case preserved).</summary>
        public string NewStatus { get; init; }
    }
}
