namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Shows a "character joined" UI notification without actually adding them
    /// to the party. Used when the recruitment happens via a different mechanism
    /// but the player still needs to see the visual confirmation.
    /// </summary>
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
