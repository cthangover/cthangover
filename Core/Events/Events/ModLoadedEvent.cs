namespace Cthangover.Core.Events
{
    [EventKey("mod_loaded")]
    public class ModLoadedEvent : ModEvent
    {
        public string ModId { get; init; }
    }
}
