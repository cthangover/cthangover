namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus when a tag is added to or removed from a quest (see QuestBase.AddTag/RemoveTag).</summary>
    [EventKey("quest_tag_changed")]
    public class QuestTagChangedEvent : ModEvent
    {
        /// <summary>Id of the quest whose tag set changed.</summary>
        public string QuestId { get; init; }
        /// <summary>The tag that was added or removed.</summary>
        public string Tag { get; init; }
        /// <summary>True when the tag was added, false when removed.</summary>
        public bool Added { get; init; }
    }
}
