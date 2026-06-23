namespace Cthangover.Core.Actions.Atomic
{
    public class CharacterSendNotificationAction : IScenarioAction
    {
        public string Name => "character.send_notification";

        public void Run(IActionContext ctx)
        {
            var typeRaw = ctx.GetParam("type");

            if (string.IsNullOrEmpty(typeRaw))
            {
                ctx.Log("EVENT", "CharacterSendNotificationAction: missing 'type' variable");
                return;
            }

            ctx.Character.SendNotification(typeRaw);
            ctx.Log("EVENT", $"CharacterSendNotificationAction: sent notification for '{typeRaw}'");
        }
    }
}
