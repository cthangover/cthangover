namespace Cthangover.Core.Actions.Atomic
{
    public class QuestSetStatusAction : IScenarioAction
    {
        public string Name => "quest.set_status";

        public void Run(IActionContext ctx)
        {
            var questId = ctx.GetParam("quest_id");
            var statusRaw = ctx.GetParam("status");

            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(statusRaw))
            {
                ctx.Log("EVENT", "QuestSetStatusAction: missing 'quest_id' or 'status' variable");
                return;
            }

            ctx.Quests.SetStatus(questId, statusRaw);
            ctx.Log("QUEST", $"QuestSetStatusAction: {questId}.Status = {statusRaw}");
        }
    }
}
