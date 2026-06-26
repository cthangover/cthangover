namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Sets the global state of a quest (e.g. "Active", "Completed", "Failed").
    /// The "status" param is parsed via Enums&lt;QuestStatus&gt;.Parse which is
    /// case-sensitive — invalid status strings are caught by QuestServiceImpl's
    /// TryGet pattern and logged as errors rather than crashing the dialog.
    /// </summary>
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
