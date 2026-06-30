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
        /// <summary>
        /// Registered as "quest.set_data_status" — sets the numeric
        /// progress level within a quest (quest.Data.Status). This is the
        /// incremental counter (e.g. "2/5 wolves killed"), separate from
        /// the global quest state managed by QuestSetStatusAction. The
        /// "level" dialog variable is parsed as int with no fallback —
        /// scenario authors must ensure it's always a valid integer.
        /// </summary>
        public string Name => "quest.set_data_status";

        /// <summary>
        /// Reads "quest_id" and "level" from dialog variables, parses
        /// level as int, and delegates to ctx.Quests.SetDataStatus. No
        /// bounds checking on the level value — the quest's data object
        /// is responsible for validating the range. Missing quests are
        /// silently skipped. Returns early with a warning if either
        /// variable is missing.
        /// </summary>
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
