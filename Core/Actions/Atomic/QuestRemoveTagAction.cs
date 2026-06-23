namespace Cthangover.Core.Actions.Atomic
{
    public class QuestRemoveTagAction : IScenarioAction
    {
        public string Name => "quest.remove_tag";

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
