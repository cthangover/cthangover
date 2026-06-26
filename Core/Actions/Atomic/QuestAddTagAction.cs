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
        public string Name => "quest.add_tag";

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
