namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published before a notification banner is created.
    /// Allows intercepting/modifying notifications.
    /// </summary>
    [EventKey("notification_requested")]
    public class NotificationRequestedEvent : ModEvent
    {
        public string Text { get; init; }
        public string Category { get; init; }
    }
}
