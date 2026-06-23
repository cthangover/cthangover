namespace Cthangover.Core.Actions.Atomic
{
    public class QuestSetDataStatusAction : IScenarioAction
    {
        public string Name => "quest.set_data_status";

        public void Run(IActionContext ctx)
        {
            var questId = ctx.GetParam("quest_id");
            var levelRaw = ctx.GetParam("level");

            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(levelRaw))
            {
                ctx.Log("EVENT", "QuestSetDataStatusAction: missing 'quest_id' or 'level' variable");
                return;
            }

            var level = int.Parse(levelRaw);
            ctx.Quests.SetDataStatus(questId, level);
            ctx.Log("QUEST", $"QuestSetDataStatusAction: {questId}.Data.Status = {level}");
        }
    }
}
