namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Sets the numeric progress level within a quest (quest.Data.Status).
    /// Unlike QuestSetStatusAction which changes the global quest state
    /// (Active/Completed), this handles incremental progress — e.g. "kill 3/5
    /// enemies". The "level" param is parsed as int (no fallback) so scenario
    /// authors must ensure the value is always a valid integer.
    /// </summary>
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
