namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Triggers a UI notification for a quest (e.g. "Quest Updated" popup).
    /// This is the visual feedback companion to quest state changes — the
    /// notification itself doesn't change quest state, it just informs the
    /// player that something happened. Call this after SetStatus/SetDataStatus
    /// to make the UI react.
    /// </summary>
    public class QuestSendNotificationAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "quest.send_notification" — triggers a UI popup
        /// for a quest (e.g. "Quest Updated" or "New Quest"). This is the
        /// visual companion to state changes — call after SetStatus or
        /// SetDataStatus so the player sees the update. The notification
        /// itself does not modify quest state.
        /// </summary>
        public string Name => "quest.send_notification";

        /// <summary>
        /// Reads the "quest_id" variable and delegates to
        /// ctx.Quests.SendNotification. The notification content is
        /// derived from the quest's current state (title, description,
        /// status) — ensure the quest state is updated before calling
        /// this. Missing quests are silently skipped via TryGet.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var questId = ctx.GetParam("quest_id");

            if (string.IsNullOrEmpty(questId))
            {
                ctx.Log("EVENT", "QuestSendNotificationAction: missing 'quest_id' variable");
                return;
            }

            ctx.Quests.SendNotification(questId);
            ctx.Log("QUEST", $"QuestSendNotificationAction: sent notification for '{questId}'");
        }
    }
}
