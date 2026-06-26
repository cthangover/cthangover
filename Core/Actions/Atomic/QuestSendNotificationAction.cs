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
        public string Name => "quest.send_notification";

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
