namespace Cthangover.Core.Events
{
    [EventKey("dialog_ended")]
    public class DialogEndedEvent : ModEvent
    {
        public string DialogId { get; init; }
    }
}
