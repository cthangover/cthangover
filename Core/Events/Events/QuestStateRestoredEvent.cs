namespace Cthangover.Core.Events
{
    /// <summary>
    /// Marker fired once after a save file's quest state is fully restored by
    /// <see cref="Cthangover.Core.Quests.QuestFactory.SetAll"/>. Published a single
    /// time after the whole restore loop (not per-quest) to invalidate cached
    /// condition results without generating a per-quest event storm.
    /// </summary>
    [EventKey("quest_state_restored")]
    public class QuestStateRestoredEvent : ModEvent
    {
    }
}
