namespace Cthangover.Core.Events
{
    [EventKey("dialog_started")]
    public class DialogStartedEvent : ModEvent
    {
        public string DialogId { get; init; }
        public string Speaker { get; init; }
    }
}
