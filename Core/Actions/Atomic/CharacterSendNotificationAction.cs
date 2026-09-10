namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Shows a "character joined" UI notification without actually adding them
    /// to the party. Used when the recruitment happens via a different mechanism
    /// but the player still needs to see the visual confirmation.
    /// </summary>
    public class CharacterSendNotificationAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "character.send_notification" — shows a "character
        /// joined" UI popup without modifying the party roster. Used when
        /// recruitment happens through external means but the player needs
        /// visual confirmation, or when re-triggering a notification at a
        /// specific story beat.
        /// </summary>
        public string Name => "character.send_notification";

        /// <summary>
        /// Reads the "type" variable and triggers the join notification
        /// via CharacterData.SendAddNotification. The notification system
        /// tracks which characters have already been notified to avoid
        /// duplicate popups for the same character.
        /// </summary>
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
