namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Removes a string tag from a quest. Paired with QuestAddTagAction for
    /// reversible quest state. Used in branching scenarios where a tag marks
    /// a temporary condition that should be cleared when resolved.
    /// </summary>
    public class QuestRemoveTagAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "quest.remove_tag" — the inverse of
        /// QuestAddTagAction. Removes a tag from a quest to clear
        /// temporary conditions (e.g. removing a "witnessed_event" tag
        /// after the event has been resolved). Both "quest_id" and "tag"
        /// are required.
        /// </summary>
        public string Name => "quest.remove_tag";

        /// <summary>
        /// Reads "quest_id" and "tag" from dialog variables and delegates
        /// to ctx.Quests.RemoveTag. Removing a non-existent tag is a
        /// no-op at the collection level. Missing quests are silently
        /// skipped via TryGet.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var questId = ctx.GetParam("quest_id");
            var tag = ctx.GetParam("tag");

            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(tag))
            {
                ctx.Log("EVENT", "QuestRemoveTagAction: missing 'quest_id' or 'tag' variable");
                return;
            }

            ctx.Quests.RemoveTag(questId, tag);
            ctx.Log("QUEST", $"QuestRemoveTagAction: {questId}.RemoveTag('{tag}')");
        }
    }
}
