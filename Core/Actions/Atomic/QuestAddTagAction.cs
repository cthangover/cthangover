namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Adds a string tag to a quest. Tags are used for conditional logic in
    /// scenario scripts — a quest can be checked for tag presence to branch
    /// dialog. The tag is added via QuestBase.AddTag, which likely stores it
    /// in the quest's save data.
    /// </summary>
    public class QuestAddTagAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "quest.add_tag" — attaches a string tag to a
        /// quest for use in conditional logic. Tags are queried by the
        /// scenario DSL's "has_tag" condition to branch dialog or gate
        /// quest progression. Both "quest_id" and "tag" dialog variables
        /// are required — returns early with a warning if either is
        /// missing.
        /// </summary>
        public string Name => "quest.add_tag";

        /// <summary>
        /// Reads "quest_id" and "tag" from dialog variables and delegates
        /// to ctx.Quests.AddTag. Routes through QuestServiceImpl.TryGet —
        /// missing quests are silently skipped. Tags are persisted with
        /// save data and survive game restart.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var questId = ctx.GetParam("quest_id");
            var tag = ctx.GetParam("tag");

            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(tag))
            {
                ctx.Log("EVENT", "QuestAddTagAction: missing 'quest_id' or 'tag' variable");
                return;
            }

            ctx.Quests.AddTag(questId, tag);
            ctx.Log("QUEST", $"QuestAddTagAction: {questId}.AddTag('{tag}')");
        }
    }
}
