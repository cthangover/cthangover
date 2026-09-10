namespace Cthangover.Core.Events
{
    [EventKey("inventory_changed")]
    public class InventoryChangedEvent : ModEvent
    {
        public string ItemId { get; init; }
        public int Delta { get; init; }
        public int NewCount { get; init; }
    }
}
