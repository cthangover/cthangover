namespace Cthangover.Core.Items
{
    /// <summary>
    /// Concrete inventory slot holding a shared <c>IItem</c> reference
    /// and a mutable stack count. The item reference is shared across
    /// every container that holds the same item type — only the count
    /// is slot-specific — which keeps memory footprint low when
    /// multiple stacks of the same item exist (e.g. 3 stacks of arrows
    /// in different inventory rows).
    /// </summary>
    public class ItemContainer : IItemContainer
    {
        /// <summary>The shared item definition held by this slot (one instance per item type, not per stack).</summary>
        public IItem Item { get; set; }
        /// <summary>Stack size; <see cref="Inventory"/> deletes the container when it reaches zero.</summary>
        public int Count { get; set; }
    }

}
