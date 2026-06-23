namespace Cthangover.Core.Actions.Atomic
{
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
