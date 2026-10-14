namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published before a notification banner is created.
    /// Allows intercepting/modifying notifications.
    /// </summary>
    [EventKey("notification_requested")]
    public class NotificationRequestedEvent : ModEvent
    {
        /// <summary>Notification text; the panel buffers it until a visible slot is free.</summary>
        public string Text { get; init; }
        /// <summary>Optional grouping label used by UI consumers.</summary>
        public string Category { get; init; }
    }
}
