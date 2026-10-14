namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus after every successful inventory add/remove; mirrors the inventory Change callback so bus subscribers get the same signal.</summary>
    [EventKey("inventory_changed")]
    public class InventoryChangedEvent : ModEvent
    {
        /// <summary>Template id of the changed item.</summary>
        public string ItemId { get; init; }
        /// <summary>Signed quantity change (positive = added, negative = removed).</summary>
        public int Delta { get; init; }
        /// <summary>Total count of the item after the change.</summary>
        public int NewCount { get; init; }
    }
}
