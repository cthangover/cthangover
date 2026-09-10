namespace Cthangover.Core.Events
{
    /// <summary>
    /// Fired when a quest's numeric progression level (<c>QuestData.Status</c>)
    /// changes. Published by <see cref="Cthangover.Core.Quests.QuestData.SetStatus"/>
    /// at the exact point of mutation, and only when the value actually changed.
    /// Carries no quest ID because <c>QuestData</c> does not know its owner — the
    /// marker is enough to invalidate cached condition results, which re-read live
    /// quest state on the next evaluation.
    /// </summary>
    [EventKey("quest_data_status_changed")]
    public class QuestDataStatusChangedEvent : ModEvent
    {
        /// <summary>Previous numeric status value.</summary>
        public int Old { get; init; }

        /// <summary>New numeric status value.</summary>
        public int New { get; init; }
    }
}
