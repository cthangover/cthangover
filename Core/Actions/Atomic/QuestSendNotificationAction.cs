namespace Cthangover.Core.Actions.Atomic
{
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
