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
        /// <summary>
        /// Registered as "quest.set_status" — sets the global lifecycle
        /// state of a quest (Active, Completed, Failed, etc.). The
        /// "status" dialog variable is parsed via
        /// Enums&lt;QuestStatus&gt;.Parse — case-sensitive and must match
        /// a QuestStatus enum value exactly. Invalid status strings are
        /// caught by QuestServiceImpl and logged as errors without
        /// crashing the dialog.
        /// </summary>
        public string Name => "quest.set_status";

        /// <summary>
        /// Reads "quest_id" and "status" from dialog variables and
        /// delegates to ctx.Quests.SetStatus. Missing quests are silently
        /// skipped. Invalid status values are logged and ignored.
        /// </summary>
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
