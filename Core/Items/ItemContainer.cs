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
        public IItem Item { get; set; }
        public int Count { get; set; }
    }

}
